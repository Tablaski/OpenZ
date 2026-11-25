using System;

namespace OpenZ.Sim
{
    internal static class ZAiDifficulty
    {
        internal static bool IsValid(ZBotDifficulty level) => level >= ZBotDifficulty.Normal && level <= ZBotDifficulty.Zod;
        internal static bool IsModern(ZBotDifficulty level) => level >= ZBotDifficulty.Noob && level <= ZBotDifficulty.Zod;
        internal static ZBotDifficulty InGeneration(ZBotDifficulty level, bool modern) =>
            IsModern(level) == modern ? level : modern ? ZBotDifficulty.Skilled : ZBotDifficulty.Normal;
        internal static readonly ZBotDifficulty[] Modern = { ZBotDifficulty.Noob, ZBotDifficulty.Alright, ZBotDifficulty.Skilled, ZBotDifficulty.Zod };

        internal static string Hint(ZBotDifficulty level) => level switch
        {
            ZBotDifficulty.Noob => "Slow openings, scattered troops and frequent mistakes.",
            ZBotDifficulty.Alright => "Quick captures and local defense with small groups.",
            ZBotDifficulty.Skilled => "Uses range, reinforcements and coordinated attacks.",
            ZBotDifficulty.Zod => "Micromanages fronts and looks for openings at forts.",
            ZBotDifficulty.Easy => "The classic opponent with a slower start and smaller attacks.",
            ZBotDifficulty.Hard => "The classic opponent with more aggressive attacks.",
            _ => "The classic OpenZ opponent."
        };
    }

    internal sealed class ZAiTuning
    {
        internal readonly int Apm, Burst, UnitsPerTurn;
        internal readonly double Opening, Tactics, Strategy, Memory, Attention, Help, AllForOne;
        internal readonly double MissProduction, MissVehicle, MissGrenades, IdleChance, BadFight, CancelChance, SalvageChance;
        internal readonly double EngageRatio, HoldRatio, ReengageRatio, WithdrawalChance, DefenseCheckChance, FlagCheckChance, ApproachSupportWeight;
        internal static ZAiTuning For(ZBotDifficulty level) => profiles[(int)level - (int)ZBotDifficulty.Noob];

        static readonly ZAiTuning[] profiles = {
            new ZAiTuning(16, 2, 2, 5, 1.20, 8, 5, 8, .25, .45, .40, .30, .35, .25, .35, 0, 0,
                engageRatio: .78, holdRatio: .78, reengageRatio: 1, withdrawalChance: 0, defenseCheckChance: 0, flagCheckChance: 0, approachSupportWeight: 0),
            new ZAiTuning(35, 3, 4, 2, .80, 6, 12, 4, .60, .80, .15, .10, .15, .08, .15, .35, .25,
                engageRatio: .78, holdRatio: .74, reengageRatio: 1, withdrawalChance: 1, defenseCheckChance: 1, flagCheckChance: 0, approachSupportWeight: 0),
            new ZAiTuning(80, 5, 7, .9, .45, 5, 24, 1.5, .95, 1, 0, 0, 0, 0, 0, .85, 1,
                engageRatio: .96, holdRatio: .90, reengageRatio: 1.2, withdrawalChance: 1, defenseCheckChance: 1, flagCheckChance: 1, approachSupportWeight: 1),
            new ZAiTuning(140, 7, 10, .55, .25, 2, 35, .5, 1, 1, 0, 0, 0, 0, 0, 1, 1,
                engageRatio: .96, holdRatio: .90, reengageRatio: 1.2, withdrawalChance: 1, defenseCheckChance: 1, flagCheckChance: 1, approachSupportWeight: 1)
        };

        ZAiTuning(int apm, int burst, int units, double opening, double tactics, double strategy, double memory,
            double attention, double help, double allForOne, double missProduction, double missVehicle,
            double missGrenades, double idleChance, double badFight, double cancelChance, double salvageChance,
            double engageRatio, double holdRatio, double reengageRatio, double withdrawalChance, double defenseCheckChance,
            double flagCheckChance, double approachSupportWeight)
        {
            Apm = apm; Burst = burst; UnitsPerTurn = units;
            Opening = opening; Tactics = tactics; Strategy = strategy; Memory = memory; Attention = attention;
            Help = help; AllForOne = allForOne;
            MissProduction = missProduction; MissVehicle = missVehicle; MissGrenades = missGrenades;
            IdleChance = idleChance; BadFight = badFight; CancelChance = cancelChance; SalvageChance = salvageChance;
            EngageRatio = engageRatio; HoldRatio = holdRatio; ReengageRatio = reengageRatio; WithdrawalChance = withdrawalChance;
            DefenseCheckChance = defenseCheckChance; FlagCheckChance = flagCheckChance; ApproachSupportWeight = approachSupportWeight;
        }
    }

    internal static class ZAiGeometry
    {
        internal static double Distance(ZObject a, ZObject b) => Math.Sqrt(DistanceSquared(a, b.CenterX, b.CenterY));
        internal static long DistanceSquared(ZObject a, int x, int y)
        {
            long dx = a.CenterX - x, dy = a.CenterY - y;
            return dx * dx + dy * dy;
        }
    }
}
