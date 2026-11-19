#ifndef OPENZ_WATER_BLAST_INCLUDED
#define OPENZ_WATER_BLAST_INCLUDED

// BLAST STAMPS. A rocket, missile, shell or bullet that lands in the water (View/OzWaterBlast.cs) drops one stamp at its world point; the
// water works out the stamp's age from the time it was dropped. Nothing is painted as a ring: the stamp returns
//   xy = a SLOPE the water adds to its own waves: three crests behind a front that travels OUTWARD and slows, the crests moving apart and
//        fading. They show as the water's own light, reflection and glints. The front is not a circle (lobes that differ per stamp).
//        Every crest is an open ARC: it lives on one side of the stamp only (over 43 % of the turn it is exactly 0, by its seed), each
//        crest turned a little against the next, cut into uneven pieces round the turn and broken by gaps that lie still in the
//        water. Never a closed line, never an even one. Held low while it is still small (the first quarter of its life).
//   z  = churn 0..1: the caller lays foam flecks there through its moving noise (the level's foam colour, darker at night). An uneven
//        patch that grows a little, drifts, and erodes into flecks as it dies: never a flat pale disc.
//   w  = the blast hole 0..1: for the first half second the water where the charge went in is darker and smooth (the caller darkens the
//        body and calms the small waves), its edge uneven.
// The same pattern as the wake stamps of kit bizon_ripple_r6 (OzWaterWake in OpenZWaterContact.hlsl): a disturbance is DATA the water reads.
//   _OZBlastA[28]  x, z, the time it was dropped (_Time.y), reach m (how far its front runs; 0 = an empty place)
//   _OZBlastB[28]  the water's y there, life s, its seed 0..1, the slope of its first crest
//   _OZBlastC[28]  foam 0..1, hole 0..1, drift of the foam (x, z) m per s
//   _OZBlastInfo   x = how many places to read, y = the time after which every stamp is dead (unset = 0 = nothing: the water as before)
// Cost: no living stamp = one compare. Else per place: dead or not yet = a subtract and a compare; living but out of reach ~10; a stamp
// that reaches the pixel ~90. No texture, no gradient.
#define OZ_BLAST_MAX 28
float4 _OZBlastA[OZ_BLAST_MAX];
float4 _OZBlastB[OZ_BLAST_MAX];
float4 _OZBlastC[OZ_BLAST_MAX];
float4 _OZBlastInfo;

half4 OzWaterBlast(float3 positionWS, float t, float mPerPx)
{
    half4 o = half4(0.0, 0.0, 0.0, 0.0);
    if (_OZBlastInfo.x < 0.5 || t >= _OZBlastInfo.y) return o;
    int n = (int)min(_OZBlastInfo.x, (float)OZ_BLAST_MAX);
    [loop] for (int i = 0; i < n; i++)
    {
        float4 A = _OZBlastA[i];
        float4 B = _OZBlastB[i];
        float age = t - A.z;
        if (A.w <= 0.0 || age <= 0.0 || age >= B.y) continue;                          // an empty place, not yet, dead
        float2 q = positionWS.xz - A.xy;
        float R = A.w;
        float r2 = dot(q, q);
        if (r2 > R * R * 1.5 || abs(positionWS.y - B.x) > 0.4) continue;               // out of its reach / another water height
        float4 C = _OZBlastC[i];
        float u = age / B.y;
        float r = sqrt(r2);
        float2 dir = q / max(r, 1e-3);
        float sd = B.z;
        float2 e1, e2;
        sincos(sd * 6.2831853, e1.y, e1.x);
        sincos(sd * 17.3 + 1.9, e2.y, e2.x);
        float c1 = dot(dir, e1), c2 = dot(dir, e2);
        float lobe = 0.10 * c1 + 0.07 * (2.0 * c2 * c2 - 1.0) + 0.05 * c1 * (4.0 * c1 * c1 - 3.0);   // -0.22 .. 0.22: the front is no circle
        float rr = r * (1.0 + lobe);
        float ki = 10.5 / R;                                                           // a pattern that lies still in the water, sized to the stamp
        float nz = sin(q.x * ki + sd * 37.0) * sin(q.y * ki * 0.81 - sd * 23.0) + 0.3 * sin((q.x + q.y) * ki * 2.05 + sd * 11.0);   // -1.3 .. 1.3

        // the crests: a front that runs out fast and slows, three crests behind it, the first full, the next weaker, moving apart
        float lam = max(R * 0.15, 0.28) * (1.0 + 0.8 * u);                             // never under 0.28 m: a small stamp's crest stays over ~3 pixels at the game camera
        float rf = R * (0.10 + 0.90 * (1.0 - (1.0 - u) * (1.0 - u)));
        float x = (rf - rr) / lam;                                                     // 0 at the front, + behind it
        if (x > -0.5 && x < 3.4)
        {
            float env = smoothstep(-0.5, 0.0, x) * (1.0 - smoothstep(0.5, 3.4, x));
            // an open ARC, never a loop: each crest lives on one side only. cos of the angle to the stamp's own direction, each crest turned
            // 26 degrees against the next (the turn changes where sin(6.28 x) is 0, so nothing jumps); 0.2 + 0.9 cos is under 0 on 43 % of
            // the turn = a dead sector every seed has. Inside the live side: three uneven pieces (cos 3a) and five notches (cos 5a).
            float kc = clamp(floor(x), 0.0, 2.0) - 1.0;
            float2 tr;
            sincos(kc * 0.45, tr.y, tr.x);
            float ca = c1 * tr.x + dot(dir, float2(-e1.y, e1.x)) * tr.y;
            float am = saturate(0.2 + 0.9 * ca) * (0.62 + 0.38 * c2 * (4.0 * c2 * c2 - 3.0));
            float c5 = c2 * c2;
            c5 = c2 * (16.0 * c5 * c5 - 20.0 * c5 + 5.0);                               // cos 5b (b = the angle to the stamp's second direction, so the arc is not mirrored): notches whose size follows the crest's radius, fine near the centre
            float gap = smoothstep(-0.95, -0.35, c5 + 0.5 * nz);                        // + the gaps that lie still in the water (they take over further out)
            float fade = (1.0 - u) * sqrt(1.0 - u) * smoothstep(0.0, 0.05, u) * lerp(0.3, 1.0, smoothstep(0.04, 0.25, u));   // low while the crest is still a small arc round the foam
            float aaK = saturate((lam / mPerPx - 2.5) * 0.4);                          // no shimmer where a crest is under ~3 pixels
            o.xy += (half2)(dir * (sin(6.2831853 * x) * env * am * gap * fade * aaK * B.w));
        }

        // the blast hole: dark smooth water where the charge went in, about half a second, an uneven edge
        float hr = R * 0.20 * (1.0 + 0.22 * nz);
        float hole = (1.0 - smoothstep(0.35 * hr, hr, rr)) * (1.0 - smoothstep(0.16, 0.55, age)) * smoothstep(0.0, 0.03, age) * C.y;
        o.w = max(o.w, (half)hole);

        // the foam: an uneven patch that grows a little, drifts and dies; the caller erodes it into flecks through its own moving noise
        float2 qd = q - C.zw * age;
        float nf = sin(qd.x * ki * 1.3 + sd * 31.0) * sin(qd.y * ki * 1.05 - sd * 17.0) + 0.5 * sin((qd.x + qd.y) * ki * 2.4 + sd * 7.0);   // -1.5 .. 1.5
        float rn = R * (0.26 + 0.34 * sqrt(u)) * (0.72 + 0.26 * nf);                   // its edge moves in and out: never a round stamp
        float l = length(qd) * (1.0 + 1.5 * lobe);
        float f = (1.0 - smoothstep(0.45 * rn, rn, l)) * (1.0 - u) * smoothstep(0.0, 0.04, u) * C.x;
        o.z = max(o.z, (half)(f * (1.0 - hole)));
    }
    return o;
}

#endif
