// Where a wreck, a wreck piece or a swimming robot cuts the water surface. Used by OpenZ/WaterV4 (L02 river) and OpenZ/WaterStylized (L17 harbour,
// L16 swamp). Two lists the game sets every frame as shader globals (outside the material CBUFFER: SRP Batcher safe; unset = 0 = nothing):
//   _OZWadePoints[16] (x, water y, z, strength 0..1) and _OZWadeCount: wading / swimming robots (View/OpenZWading.cs, published since 1.0, read by
//       no shader until this kit)
//   _OZWetBoxA[16] (centre x, centre z, half length, half width), _OZWetBoxB[16] (cos yaw, sin yaw, water y, depth its bottom reaches under the
//       surface) and _OZWetBoxCount: the pieces the water landing rule holds (View/OzWaterLanding.cs: wreck hulls, turrets, wreck pieces, chunks),
//       as their footprint at the waterline (a rounded box)
// OzWaterContacts returns
//   x = ring: a thin soft foam band hugging the outline where the object cuts the surface, plus one slow faint ripple ring travelling out
//       (0..1; the caller breaks it up with its foam noise and lights it as its foam paint)
//   y = over: this water pixel lies over the submerged part of a contact (inside its waterline outline, and the scene point behind it is no
//       deeper than the contact reaches): the caller draws the water there with the clearer object curve (the part under the surface shows faintly)
// Cost: a loop over the live contacts only (usually 0 to 6, at most 32), about 30 ALU each, skipped at once on another water height or > ~1.3 m away.
#ifndef OPENZ_WATER_CONTACT_INCLUDED
#define OPENZ_WATER_CONTACT_INCLUDED

#define OZ_WATER_CONTACTS 16
#define OZ_WADE_RING_R 0.34     // a robot's body at its waterline (m): the ring hugs this circle
#define OZ_WADE_OVER_R 0.24     // the clearer water over a swimmer: only over its body core (the bed beside its legs keeps the water's own curve)
#define OZ_WADE_UNDER 1.4       // how deep a swimmer reaches (m): its legs and feet

float4 _OZWadePoints[OZ_WATER_CONTACTS];
float _OZWadeCount;
float4 _OZWetBoxA[OZ_WATER_CONTACTS];
float4 _OZWetBoxB[OZ_WATER_CONTACTS];
float _OZWetBoxCount;

// swimming robot or a hull dipping a track is NEVER a closed even line (the same rule as the Bizon's, kit bizon_ripple_r6):
//  - the foam at the outline is broken uneven flecks: one side of every contact has none (exactly 0 on 43 % of the turn; which side by the
//    contact's own phase, turning slowly), the live side is cut into uneven pieces and notches, and the flecks come from a pattern that lies
//    still in the water (OzContactBreak, OzContactNoise);
//  - the ripples that travel out from it are no longer foam paint (a pale line round the outline): they are the water's own SLOPE (its
//    light, reflection and glints), open notched arcs that run outward and fade (OzContactRipple), which the water shader adds to its other
//    ripples through OzWaterContactSlope(). Under ~3 pixels they fade out (no shimmer at the far zoom).
#define OZ_CONTACT_RIP_SLOPE 0.3
static half2 ozContactSlope = half2(0.0, 0.0);

float OzContactNoise(float2 p, float ph)
{
    return sin(p.x * 7.3 + ph * 19.0) * sin(p.y * 6.1 - ph * 13.0) + 0.4 * sin((p.x - p.y) * 13.7 + ph * 7.0);   // -1.4 .. 1.4, lies still in the water
}

// 0..1 round the outline (ang = the angle round the contact, a0 = the direction of its dead side, nz = OzContactNoise there)
half OzContactBreak(float ang, float a0, float nz)
{
    float side = saturate(0.2 + 0.9 * cos(ang - a0));                                  // exactly 0 on 43 % of the turn: never a closed line
    float pieces = 0.55 + 0.45 * sin(3.0 * ang - 1.7 * a0);                            // uneven pieces on the live side
    float gap = smoothstep(-0.9, -0.3, cos(5.0 * ang + 2.3 * a0) + 0.6 * nz);         // notches, and gaps that lie still in the water
    return (half)(side * pieces * gap);
}

// one travelling ripple as SLOPE: a crest and its trough at rr m outside the outline, an open notched arc turned a little as it runs out,
// fading in at the outline and out as it goes (u = its age 0..1, dir = outward, k = strength x outside)
void OzContactRipple(float s, float rr, float u, float ang, float a0, float nz, float2 dir, float aa, half k)
{
    float lam = 0.3 * (1.0 + 0.6 * u);                                                 // wider as it goes; never under 0.3 m
    float x = (s - rr) / lam;
    if (abs(x) >= 1.0) return;
    float bump = sin(3.14159265 * x) * (1.0 - x * x);
    float arc = saturate(0.2 + 0.9 * cos(ang - a0 - 0.6 - 1.2 * u)) * smoothstep(-0.9, -0.3, cos(5.0 * ang - 1.3 * a0 + 2.0) + 0.6 * nz);
    float fade = (1.0 - u) * (1.0 - u) * smoothstep(0.0, 0.12, u);
    float aaK = saturate((lam / aa - 2.5) * 0.4);                                     // no shimmer where it is under ~3 pixels
    ozContactSlope += (half2)(dir * (bump * arc * fade * aaK * OZ_CONTACT_RIP_SLOPE * (float)k));
}

// the slope of every contact's ripples at this pixel (OzWaterContacts has run before it in both water shaders); 0 with no contact near
half2 OzWaterContactSlope() { return ozContactSlope; }

// one contact: s = signed distance (m) from its waterline outline (+ outside), sOver = the same for the "over" mask, under = how deep it reaches,
// k = strength, ph = its own phase (rings never in lockstep)
void OzContactOne(float s, float sOver, float d, float under, half k, float ph, float t, float aa, float ringW, float reach, float speed,
                  float ang, float2 dir, float3 positionWS, inout half ring, inout half over)   // 1.5 kit water_rings_rest_r6: + the angle round it, outward, the pixel
{
    half outside = (half)smoothstep(-aa, aa, s);
    float a0 = 6.2831853 * ph + 0.04 * t;   // 1.5 kit water_rings_rest_r6: the dead side of its outline, turning slowly
    float nz = OzContactNoise(positionWS.xz, ph);
    float wN = 0.7 + 0.3 * sin(4.0 * ang + 5.3 * ph) + 0.2 * sin(7.0 * ang - 2.9 * ph);   // 1.5 kit water_rings_rest_r6: the band's width varies round the outline
    half band = outside * (half)(1.0 - smoothstep(0.0, ringW * wN + aa, s));          // the foam band right at the outline, soft outward
    band *= OzContactBreak(ang, a0, nz) * (half)smoothstep(-0.35, 0.45, OzContactNoise(positionWS.xz * 2.1 + 3.7, ph + 0.31));   // 1.5 kit water_rings_rest_r6: broken uneven flecks, never a closed line
    float u = frac(t * speed + ph);
    OzContactRipple(s, 0.06 + u * reach, u, ang, a0, nz, dir, aa, k * outside);   // 1.5 kit water_rings_rest_r6: the ripple travels out as the water's slope (an open arc), no pale line
    ring = max(ring, k * band);
    over = max(over, (half)((1.0 - smoothstep(-aa, aa, sOver)) * (1.0 - smoothstep(under, under + 0.12, d))));
}

// follows a soft organic waterline instead of the footprint's rounded box: an ellipse round the footprint (a little fuller than an ellipse,
// never a straight edge), its radius broken by slow drifting angular noise, a soft noisy foam edge of varying width, two gentle ripples
// travelling out, and the clearer water over the submerged part the same shape just inside the ring. _OZContactBoxy = 1: the old rounded box
// (proof only; unset = 0 = organic; View/OzWaterRingShape.cs). Wading robots keep their round ring (OzContactOne, unchanged).
float _OZContactBoxy;

// signed distance (m, + outside) from the organic outline round a footprint of half size h (in its own axes: lq); ang = the angle round it
float OzContactOrganicS(float2 lq, float2 h, float ph, float t, out float ang)
{
    float2 R = h * 1.1 + 0.14;                                                          // the hull's waterline and a little water round it
    float L = length(lq);
    float2 u = lq / max(L, 1e-4);
    float2 au = abs(u) / R + 1e-6;
    float rOut = 1.0 / pow(pow(au.x, 2.4) + pow(au.y, 2.4), 1.0 / 2.4);                // the outline's radius in this direction
    ang = atan2(lq.y / R.y, lq.x / R.x + 1e-6);
    float wob = 0.55 * sin(3.0 * ang + 6.2832 * ph + 0.23 * t) + 0.30 * sin(5.0 * ang - 4.1 * ph - 0.37 * t) + 0.15 * sin(9.0 * ang + 2.7 * ph + 0.61 * t);
    float amp = 0.05 + 0.05 * min(min(R.x, R.y), 2.5);
    return L - rOut - wob * amp;
}

// one organic contact (as OzContactOne): a soft inner edge, the foam band's width varying round the outline, two ripples (the second fainter)
void OzContactOrganicOne(float s, float sOver, float d, float under, float ang, float ph, float t, float aa, float ringW, float reach, float speed,
                         float2 dir, float3 positionWS, inout half ring, inout half over)   // 1.5 kit water_rings_rest_r6: + outward, the pixel
{
    float aaS = max(aa, 0.035);
    half outside = (half)smoothstep(-aaS, aaS * 1.5, s);
    float wN = 0.75 + 0.3 * sin(7.0 * ang + 5.3 * ph + 0.4 * t) + 0.15 * sin(13.0 * ang - 2.9 * ph - 0.7 * t);
    half band = outside * (half)(1.0 - smoothstep(0.0, ringW * wN + aaS, s));
    float a0 = 6.2831853 * ph + 0.04 * t;   // 1.5 kit water_rings_rest_r6: the dead side of its outline, turning slowly
    float nz = OzContactNoise(positionWS.xz, ph);
    band *= OzContactBreak(ang, a0, nz) * (half)smoothstep(-0.35, 0.45, OzContactNoise(positionWS.xz * 2.1 + 3.7, ph + 0.31));   // 1.5 kit water_rings_rest_r6: broken uneven flecks, never a closed line
    [unroll] for (int k = 0; k < 2; k++)
    {
        float u = frac(t * speed + ph + 0.5 * k);
        OzContactRipple(s, 0.08 + u * reach * (1.0 + 0.25 * k), u, ang, a0 + 2.4 * k, nz, dir, aa, outside * (half)(k == 0 ? 1.0 : 0.6));   // 1.5 kit water_rings_rest_r6: two ripples as slope (open arcs on different sides), no pale lines
    }
    ring = max(ring, band);
    over = max(over, (half)((1.0 - smoothstep(-aaS, aaS, sOver)) * (1.0 - smoothstep(under, under + 0.12, d))));
}

half2 OzWaterContacts(float3 positionWS, float d, float t, float mPerPx, float ringW, float reach, float speed)
{
    half ring = 0.0, over = 0.0;
    ozContactSlope = half2(0.0, 0.0);   // 1.5 kit water_rings_rest_r6: the ripples' slope, summed by the contacts below
    float aa = max(mPerPx, 0.005);
    float cutoff = reach + ringW + 0.3;
    int nw = (int)min(_OZWadeCount, (float)OZ_WATER_CONTACTS);
    [loop] for (int i = 0; i < nw; i++)
    {
        float4 w = _OZWadePoints[i];
        if (abs(positionWS.y - w.y) > 0.4) continue;                                   // another water height (L16 pools)
        float r = length(positionWS.xz - w.xz);
        if (r - OZ_WADE_RING_R > cutoff) continue;
        OzContactOne(r - OZ_WADE_RING_R, r - OZ_WADE_OVER_R, d, OZ_WADE_UNDER, (half)lerp(0.6, 1.0, saturate(w.w)), frac(w.x * 0.37 + w.z * 0.61),
                     t, aa, ringW * 0.8, reach * 0.7, speed * 1.3, atan2(positionWS.z - w.z, positionWS.x - w.x), (positionWS.xz - w.xz) / max(r, 1e-3), positionWS, ring, over);   // 1.5 kit water_rings_rest_r6: the angle round the swimmer, outward
    }
    int nb = (int)min(_OZWetBoxCount, (float)OZ_WATER_CONTACTS);
    [loop] for (int j = 0; j < nb; j++)
    {
        float4 A = _OZWetBoxA[j];
        float4 B = _OZWetBoxB[j];
        if (abs(positionWS.y - B.z) > 0.4) continue;
        float2 q = positionWS.xz - A.xy;
        float2 lq = float2(dot(q, B.xy), dot(q, float2(-B.y, B.x)));                    // into the piece's own footprint axes
        float2 h = max(A.zw, 0.05);
        float rc = min(h.x, h.y) * 0.4;                                                 // rounded corners: a hull's waterline is never a sharp box
        float2 e = abs(lq) - (h - rc);
        float s = length(max(e, 0.0)) + min(max(e.x, e.y), 0.0) - rc;
        float ph = frac(A.x * 0.37 + A.y * 0.61);
        if (_OZContactBoxy < 0.5)
        {   // 1.5 kit view_notes11 N1: the organic waterline ring (OzContactOrganicS / OzContactOrganicOne above)
            float ang;
            float so = OzContactOrganicS(lq, h, ph, t, ang);
            if (so > cutoff) continue;
            OzContactOrganicOne(so, so + 0.1, d, B.w, ang, ph, t, aa, ringW, reach, speed, q / max(length(q), 1e-3), positionWS, ring, over);   // 1.5 kit water_rings_rest_r6: outward
            continue;
        }
        if (s > cutoff) continue;
        OzContactOne(s, s, d, B.w, (half)1.0, ph, t, aa, ringW, reach, speed, atan2(lq.y, lq.x), q / max(length(q), 1e-3), positionWS, ring, over);   // 1.5 kit water_rings_rest_r6: the angle round it, outward
    }
    ring *= (half)lerp(1.0, 0.5, smoothstep(0.08, 0.25, aa));                            // calmer at the overview zoom
    return half2(ring, over);
}

// stands in the water: the static pieces of level 18 (the stumps, the barge, the containers: _OZClearBoxA / B / _OZClearCount, published by
// View/OzL18WaterClear.cs as their footprint at the waterline, the wet-box format) and the contacts water_depth_soft already lists (wading
// robots, wrecks, pieces). 1 at the outline, soft to 0 at `width` m out. Only a material with _ObjectClearBand > 0 (M_Water_L18) uses it.
#define OZ_CLEAR_MAX 24
float4 _OZClearBoxA[OZ_CLEAR_MAX];
float4 _OZClearBoxB[OZ_CLEAR_MAX];
float _OZClearCount;

float OzClearBoxS(float3 positionWS, float4 A, float4 B)
{
    float2 q = positionWS.xz - A.xy;
    float2 lq = float2(dot(q, B.xy), dot(q, float2(-B.y, B.x)));
    float2 h = max(A.zw, 0.05);
    float rc = min(h.x, h.y) * 0.4;
    float2 e = abs(lq) - (h - rc);
    return length(max(e, 0.0)) + min(max(e.x, e.y), 0.0) - rc;
}

half OzWaterClearBand(float3 positionWS, float width)
{
    float w = max(width, 0.05);
    half k = 0.0;
    int nc = (int)min(_OZClearCount, (float)OZ_CLEAR_MAX);
    [loop] for (int i = 0; i < nc; i++)
    {
        float4 B = _OZClearBoxB[i];
        if (abs(positionWS.y - B.z) > 0.6) continue;
        float s = OzClearBoxS(positionWS, _OZClearBoxA[i], B);
        if (s > w) continue;
        k = max(k, (half)(1.0 - smoothstep(0.0, w, s)));
    }
    int nw = (int)min(_OZWadeCount, (float)OZ_WATER_CONTACTS);
    [loop] for (int j = 0; j < nw; j++)
    {
        float4 p = _OZWadePoints[j];
        if (abs(positionWS.y - p.y) > 0.4) continue;
        float s = length(positionWS.xz - p.xz) - OZ_WADE_RING_R;
        if (s > w * 0.5) continue;
        k = max(k, (half)((1.0 - smoothstep(0.0, w * 0.5, s)) * lerp(0.6, 1.0, saturate(p.w))));
    }
    int nb = (int)min(_OZWetBoxCount, (float)OZ_WATER_CONTACTS);
    [loop] for (int b = 0; b < nb; b++)
    {
        float4 B = _OZWetBoxB[b];
        if (abs(positionWS.y - B.z) > 0.4) continue;
        float s = OzClearBoxS(positionWS, _OZWetBoxA[b], B);
        if (s > w) continue;
        k = max(k, (half)(1.0 - smoothstep(0.0, w, s)));
    }
    return k;
}

// WAKE STAMPS. A hull that wades (the Bizon, View/OzWadeWake.cs) drops stamps into the water
// where its track ends disturb it. A stamp stays at its world point; the water works out its age from the time it was dropped. Nothing is
// painted: a ripple stamp returns a SLOPE the water adds to its own waves (so it shows as the water's own light, reflection and glints),
// a churn stamp returns how churned the water is there (the caller calms its small waves and lays a few foam flecks through its noise).
//   _OZWakeHull[4]  x, z, radius of the circle round one hull's living stamps, water y   (radius 0 = an empty place)
//   _OZWakeA[4 x 24] x, z, the time it was dropped (_Time.y), strength (a ripple arc / a wavelet: its slope; a patch: 0..1)
//   _OZWakeB[4 x 24] x, z = the way a ripple arc faces / a wavelet drifts / a patch is stretched; speed m per s: > 0 a ripple arc's front, < 0 a wavelet's drift, 0 = a churn patch; life s
//   _OZWakeC[4 x 24] a ripple arc: start radius m, crest spacing m, cos of its half width, its seed; a wavelet: its bend (1 / m), crest spacing m, the way its crests face (x, z);
//                    a patch: its stretch along B.xy (>= 1), its radius m, -, its seed
//   _OZWakeCount     how many of the four places to read (unset = 0 = nothing: the water exactly as without this kit)
// A ripple arc (standing, turning, entering): two crests behind a front that travels out, the second weaker, the spacing widening, fading;
// only an arc of it (the side that faces away from the hull), broken by gaps that lie still in the water: never a closed ring.
// A wake wavelet (driving): a short packet of two or three crests that drifts away from the path the hull drove, lengthens and fades: the
// separate feathers of a real wake. Its crest line is BENT and wavy and broken by gaps that lie still in the water, every one its own length
// (never a straight bar); the wavelets of one track end lie on one arm of the wake wedge (19.5 deg) by the way they are dropped.
// A churn patch: an uneven blob (its edge moved in and out by a pattern that lies still in the water), longer along the path than across.
// Cost: no Bizon in water = one compare. Else one circle test per wading Bizon (at most 4); only inside a circle the 24 stamps of that
// Bizon: a dead or far one costs a few ALU, a ripple that reaches the pixel ~55, a patch ~25. No texture, no gradient.
#define OZ_WAKE_HULLS 4
#define OZ_WAKE_PER 24
float4 _OZWakeHull[OZ_WAKE_HULLS];
float4 _OZWakeA[OZ_WAKE_HULLS * OZ_WAKE_PER];
float4 _OZWakeB[OZ_WAKE_HULLS * OZ_WAKE_PER];
float4 _OZWakeC[OZ_WAKE_HULLS * OZ_WAKE_PER];
float _OZWakeCount;

// xy = the slope the stamps add (world x, z), z = churn 0..1
half3 OzWaterWake(float3 positionWS, float t, float mPerPx)
{
    half3 o = half3(0.0, 0.0, 0.0);
    int nh = (int)min(_OZWakeCount, (float)OZ_WAKE_HULLS);
    [loop] for (int h = 0; h < nh; h++)
    {
        float4 H = _OZWakeHull[h];
        float2 qh = positionWS.xz - H.xy;
        if (abs(positionWS.y - H.w) > 0.4 || dot(qh, qh) > H.z * H.z) continue;       // another water height / outside this hull's circle
        [loop] for (int i = 0; i < OZ_WAKE_PER; i++)
        {
            int j = h * OZ_WAKE_PER + i;
            float4 A = _OZWakeA[j];
            float4 B = _OZWakeB[j];
            float age = t - A.z;
            if (A.w <= 0.0 || age <= 0.0 || age >= B.w) continue;                      // an empty place, not yet, dead
            float4 C = _OZWakeC[j];
            float u = age / B.w;
            float2 q = positionWS.xz - A.xy;
            if (B.z > 0.0)
            {   // a ripple arc: two crests behind a front that travels out
                float r = length(q);
                float lam = C.y * (1.0 + 0.5 * u);                                      // the crests move apart as they go
                float x = (C.x + B.z * age - r) / lam;                                  // 0 at the front, + behind it
                if (x < -0.5 || x > 1.6) continue;
                float2 dir = q / max(r, 1e-3);
                float env = smoothstep(-0.5, 0.0, x) * (1.0 - smoothstep(0.35, 1.6, x)); // the first crest full, the second weaker, then nothing
                float arc = smoothstep(C.z - 0.22, C.z + 0.22, dot(dir, B.xy));         // only the side that faces away from the hull
                float gap = smoothstep(-0.45, 0.15, sin(q.x * 2.1 + C.w * 37.0) * sin(q.y * 1.7 - C.w * 23.0) + 0.25 * sin((q.x + q.y) * 4.3 + C.w * 11.0));
                float fade = (1.0 - u) * sqrt(1.0 - u) * smoothstep(0.0, 0.1, u);
                float aaK = saturate((lam / mPerPx - 2.5) * 0.4);                       // no shimmer where a crest is under ~3 pixels
                o.xy += (half2)(dir * (sin(6.2831853 * x) * env * arc * gap * fade * aaK * A.w));
            }
            else if (B.z < 0.0)
            {   // a wake wavelet: a short bent packet of crests (they face C.zw) that drifts along B.xy, lengthens and fades
                float2 c = q - B.xy * (0.25 - B.z * age);
                float sd = frac(A.z * 7.31 + A.x * 0.173);                              // its own seed
                float lam = C.y * (1.0 + 0.4 * u);
                float sT = C.y * (0.75 + 0.9 * sd + 1.0 * u);                           // half its length along the crests: every one its own
                float d2 = dot(c, c);
                if (d2 > 7.0 * sT * sT) continue;
                float ds = dot(c, float2(-C.w, C.z));                                   // along the crest
                float dn = dot(c, C.zw) + C.x * ds * ds                                 // across it; the crest line is bent ...
                         + 0.13 * lam * sin(ds * 2.3 + sd * 40.0 + 1.7 * sin(ds * 0.9 + sd * 11.0));   // ... and wavy
                float w = exp(-dn * dn / (0.6 * lam * lam) - ds * ds / (sT * sT));
                float brk = smoothstep(-0.5, 0.2, sin(q.x * 2.3 + sd * 37.0) * sin(q.y * 1.9 - sd * 23.0) + 0.3 * sin((q.x - q.y) * 4.1 + sd * 11.0));   // gaps that lie still in the water
                float aaK = saturate((lam / mPerPx - 2.5) * 0.4);
                o.xy += (half2)(C.zw * (sin(6.2831853 * dn / lam) * w * brk * (1.0 - u) * smoothstep(0.0, 0.08, u) * aaK * A.w));
            }
            else
            {   // a churn patch: soft, uneven, longer along the path, growing a little, dying
                float2 lq = float2(dot(q, B.xy) / max(C.x, 1.0), dot(q, float2(-B.y, B.x)));
                float R = C.y * (0.75 + 0.5 * u);
                float l = length(lq);
                if (l > R * 1.15) continue;
                float nz = sin(q.x * 2.9 + C.w * 31.0) * sin(q.y * 2.3 - C.w * 17.0) + 0.5 * sin((q.x + q.y) * 5.3 + C.w * 7.0);   // -1.5 .. 1.5, lies still in the water
                float rn = R * (0.72 + 0.26 * nz);                                      // its edge moves in and out: never a round stamp
                o.z = max(o.z, (half)((1.0 - smoothstep(0.2 * rn, rn, l)) * (1.0 - u) * smoothstep(0.0, 0.12, u) * A.w));
            }
        }
    }
    return o;
}

// The wading hull itself (its footprint at the waterline, View/OzWadeWake.cs, each frame it wades): NO ring, NO line of any kind. Only
//   x = over: this water pixel lies over the submerged hull (the scene point behind it no deeper than the hull reaches): the caller draws
//       The edge of this mask is 0.4 m soft
//       Full over the hull, to nothing 0.9-1.9 m out with zero slope at both ends; that width follows a pattern that lies
//       still in the water, so nothing keeps one distance from the hull
//   z = flank: where the water's shore-foam line would hug the hull's flanks (a pale line at one distance from the hull): the caller holds
//       its foam back there (x _OZWakeLook.x; 0 = the level's shore foam at the hull as on every other object)
//   _OZWakeBoxA[4] centre x, z, half width, half length; _OZWakeBoxB[4] its right axis (x, z), water y, how deep it reaches x its fade (0 = none)
//   _OZWakeLook    x = how much of the shore foam is held back at the hull (0..1), y = the share of the clear zone (0..1)
float4 _OZWakeBoxA[OZ_WAKE_HULLS];
float4 _OZWakeBoxB[OZ_WAKE_HULLS];
float _OZWakeBoxCount;
float4 _OZWakeLook;

half3 OzWaterWakeHull(float3 positionWS, float d)
{
    half3 o = half3(0.0, 0.0, 0.0);
    int n = (int)min(_OZWakeBoxCount, (float)OZ_WAKE_HULLS);
    [loop] for (int i = 0; i < n; i++)
    {
        float4 B = _OZWakeBoxB[i];
        if (B.w <= 0.0 || abs(positionWS.y - B.z) > 0.4) continue;
        float4 A = _OZWakeBoxA[i];
        float2 q = positionWS.xz - A.xy;
        float2 lq = float2(dot(q, B.xy), dot(q, float2(-B.y, B.x)));
        float2 hh = max(A.zw, 0.05);
        float rc = min(hh.x, hh.y) * 0.4;
        float2 e = abs(lq) - (hh - rc);
        float s = length(max(e, 0.0)) + min(max(e.x, e.y), 0.0) - rc;                   // m from the hull's outline, + outside
        if (s > 2.0) continue;
        float k = saturate(B.w / 0.15);                                                 // fades in as it enters, out as it leaves
        float wide = lerp(0.9, 1.9, 0.5 + 0.5 * sin(positionWS.x * 1.3 + 0.7 * sin(positionWS.z * 0.9)) * sin(positionWS.z * 1.1 + 0.6 * sin(positionWS.x * 0.8)));
        o.x = max(o.x, (half)((1.0 - smoothstep(-0.05, 0.35, s)) * (1.0 - smoothstep(B.w, B.w + 0.2, d)) * k));
        o.y = max(o.y, (half)((1.0 - smoothstep(-0.3, wide, s)) * k));
        o.z = max(o.z, (half)((1.0 - smoothstep(0.25, 0.7, s)) * k));
    }
    o.y *= (half)saturate(_OZWakeLook.y);
    o.z *= (half)saturate(_OZWakeLook.x);
    return o;
}

#endif
