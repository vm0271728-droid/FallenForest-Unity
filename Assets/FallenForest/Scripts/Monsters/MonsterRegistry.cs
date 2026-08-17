using System.Collections.Generic;
namespace FallenForest.Monsters
{
    public static class MonsterRegistry
    {
        public static readonly HashSet<LocustAI> Locusts = new HashSet<LocustAI>();
        public static readonly HashSet<BoiledOneEncounter> Boiled = new HashSet<BoiledOneEncounter>();
    }
}
