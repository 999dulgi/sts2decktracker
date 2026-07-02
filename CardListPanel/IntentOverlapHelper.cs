using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace sts2decktracker
{
    public static class IntentOverlapHelper
    {
        public static bool IsOverlappingEnemyCreature(Control panel)
        {
            var combatRoom = NCombatRoom.Instance;
            if (combatRoom == null) return false;
            var panelRect = panel.GetGlobalRect();
            foreach (var creature in combatRoom.CreatureNodes)
            {
                if (!GodotObject.IsInstanceValid(creature)) continue;
                var entity = creature.Entity;
                if (entity == null) continue;
                if (entity.IsPlayer || entity.IsPet) continue;
                if (creature.IntentContainer == null || !GodotObject.IsInstanceValid(creature.IntentContainer)) continue;
                foreach (var child in creature.IntentContainer.GetChildren())
                {
                    if (child is not Control intentNode || !GodotObject.IsInstanceValid(intentNode)) continue;
                    var holderVariant = intentNode.Get("_intentHolder");
                    if (holderVariant.VariantType == Variant.Type.Nil) continue;
                    var holder = holderVariant.As<Control>();
                    if (holder == null || !GodotObject.IsInstanceValid(holder)) continue;
                    if (panelRect.Intersects(holder.GetGlobalRect()))
                        return true;
                }
            }
            return false;
        }
    }
}
