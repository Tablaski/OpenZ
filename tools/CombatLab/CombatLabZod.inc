#include <iostream>

bool combat_lab_synchronous_paths = false;

namespace {
struct LabUnit { int category, type; };
const LabUnit lab_units[] = {
    {VEHICLE_OBJECT, LIGHT}, {VEHICLE_OBJECT, MEDIUM}, {VEHICLE_OBJECT, HEAVY},
    {ROBOT_OBJECT, TOUGH}, {CANNON_OBJECT, GUN}, {CANNON_OBJECT, HOWITZER},
    {CANNON_OBJECT, MISSILE_CANNON}, {VEHICLE_OBJECT, MISSILE_LAUNCHER}
};

void lab_attack(ZObject *shooter, ZObject *target)
{
    waypoint order;
    order.mode = ATTACK_WP;
    order.ref_id = target->GetRefID();
    order.player_given = true;
    target->GetCenterCords(order.x, order.y);
    shooter->GetWayPointList().push_back(order);
}

bool lab_dodging(ZObject *unit)
{
    const auto &orders = unit->GetWayPointList();
    return !orders.empty() && orders.front().mode == DODGE_WP;
}
}

int ZServer::RunCombatLab(const char *settings)
{
    combat_lab_synchronous_paths = true;
    if (!zsettings.LoadSettings(settings)) return 2;
    ZMap::ServerInit();
    int seed, shooter_type, target_type, distance, terrain, health;
    std::string mode;
    while (std::cin >> seed >> mode >> shooter_type >> target_type >> distance >> terrain >> health)
    {
        if (shooter_type < 0 || shooter_type >= 8 || target_type < 0 || target_type >= 8 ||
            terrain < 0 || terrain > 2 || health < 1 || health > 100 || distance < 0 || distance > 256)
            return 3;
        if (mode != "blast" && mode != "duel" && mode != "shot-idle" && mode != "shot-move" &&
            mode != "shot-attack" && mode != "move-normal" && mode != "move-boost") return 3;

        ResetGame();
        new_damage_missile_list.clear();
        next_ref_id = 0;
        ztime.ztime = 0;
        srand(seed);
        zmap.MakeNewMap("Combat Lab", DESERT, 64, 48);
        int tile = 0;
        for (; tile < MAX_PLANET_TILES; ++tile)
        {
            const auto &info = ZMap::GetMapPaletteTileInfo(DESERT, tile);
            if (info.is_usable && info.is_passable && !info.is_effect &&
                info.is_road == (terrain == 1) && info.is_water == (terrain == 2)) break;
        }
        if (tile == MAX_PLANET_TILES) return 4;
        for (unsigned i = 0; i < 64 * 48; ++i) zmap.GetTile(i).tile = static_cast<unsigned short>(tile);
        zmap.InitPathfinding();
        srand(seed);
        const auto &t = lab_units[target_type];
        const auto &s = lab_units[shooter_type];
        auto target = CreateObject(t.category, t.type, 512, 384, BLUE_TEAM, nullptr, 0, 0, health);
        if (!target) return 5;
        int start_x, start_y;
        target->GetCenterCords(start_x, start_y);
        const int maximum = target->GetMaxHealth();
        int before = target->GetHealth(), shooter_lost = 0, radius = 0, shots = 0, hits = 0, dodged = 0, outcome = 0, travel = 0;
        if (mode == "move-normal" || mode == "move-boost")
        {
            waypoint order;
            order.mode = MOVE_WP; order.x = start_x + 320; order.y = start_y; order.player_given = true;
            target->GetWayPointList().push_back(order);
            for (int tick = 1; tick <= 200; ++tick)
            {
                ztime.ztime = tick * .01;
                ProcessObjects();
                if (tick == 1 && mode == "move-boost") target->AttemptStartRun();
            }
            int x, y;
            target->GetCenterCords(x, y);
            travel = x - start_x;
        }
        else
        {
            auto shooter = CreateObject(s.category, s.type, 512 - (mode == "blast" ? 64 : distance), 384, RED_TEAM);
            if (!shooter) return 5;
            const int shooter_id = shooter->GetRefID(), target_id = target->GetRefID();
            const int shooter_before = shooter->GetHealth();
            lab_attack(shooter, target);
            if (mode == "duel" || mode == "shot-attack") lab_attack(target, shooter);
            if (mode == "shot-move")
            {
                waypoint order;
                order.mode = MOVE_WP; order.x = start_x; order.y = start_y + 160; order.player_given = true;
                target->GetWayPointList().push_back(order);
            }
            if (mode == "duel")
            {
                for (int tick = 1; tick <= 4500 && !shooter->IsDestroyed() && !target->IsDestroyed(); ++tick)
                {
                    ztime.ztime = tick * .01;
                    auto queued = damage_missile_list.size();
                    ProcessObjects();
                    for (auto i = queued; i < damage_missile_list.size(); ++i)
                        if (damage_missile_list[i].attacker_ref_id == shooter_id && damage_missile_list[i].target_ref_id == target_id) ++shots;
                    ProcessMissiles();
                }
                shooter_lost = shooter_before - shooter->GetHealth();
                outcome = (shooter->IsDestroyed() == target->IsDestroyed()) ? 0 : (target->IsDestroyed() ? 1 : -1);
            }
            else
            {
                damage_missile shot;
                int tick = 1;
                for (; tick <= 600 && !shots && !shooter->IsDestroyed() && !target->IsDestroyed(); ++tick)
                {
                    ztime.ztime = tick * .01;
                    ProcessObjects();
                    for (const auto &missile : damage_missile_list)
                        if (missile.attacker_ref_id == shooter_id && missile.target_ref_id == target_id)
                        { shot = missile; shots = 1; break; }
                    if (!shots) ProcessMissiles();
                }
                if (shots)
                {
                    radius = shot.radius;
                    dodged = lab_dodging(target);
                    before = target->GetHealth();
                    if (mode == "blast")
                    {
                        target->GetCenterCords(shot.x, shot.y);
                        shot.x += distance;
                        dodged = 0;
                    }
                    else
                    {
                        damage_missile_list.clear();
                        new_damage_missile_list.clear();
                        for (; ztime.ztime < shot.explode_time && !target->IsDestroyed(); ++tick)
                        {
                            ztime.ztime = tick * .01;
                            ProcessObjects();
                            dodged |= lab_dodging(target);
                            damage_missile_list.clear();
                            new_damage_missile_list.clear();
                        }
                    }
                    if (!target->IsDestroyed()) ProcessMissileDamage(shot);
                    hits = target->GetHealth() < before;
                }
            }
        }
        printf("LAB %d %d %d %d %d %d %d %d %.3f %d\n", before - target->GetHealth(), maximum, shooter_lost,
            radius, shots, hits, dodged, outcome, ztime.ztime, travel);
        fflush(stdout);
    }
    return std::cin.eof() ? 0 : 3;
}
