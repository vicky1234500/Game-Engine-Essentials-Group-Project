// Enemy AI: goes through its moves in order and loops.
//   Move 1 -> Move 2 -> Move 3 -> Move 1 -> ...
// The order is the order of the Moves list on the EnemyData asset.
// e.g. Street Enforcer: Punch -> Wind Up -> Smash -> repeat
public class EnemyAI
{
    readonly EnemyData data;
    int nextIndex;

    public EnemyAI(EnemyData data) { this.data = data; }

    // The move shown as the enemy's intent (what it will do on its next turn).
    public EnemyMove ChooseMove(EnemyCombatant self)
    {
        if (data == null || data.moves.Count == 0) return null;
        return data.moves[nextIndex % data.moves.Count];
    }

    // Called after the move is done -> go to the next one.
    public void RegisterUsed(EnemyMove m)
    {
        if (data == null || data.moves.Count == 0) return;
        nextIndex = (nextIndex + 1) % data.moves.Count;
    }
}
