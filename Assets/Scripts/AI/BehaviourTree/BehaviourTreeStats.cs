namespace ChaseGame.AI.BehaviourTree
{
    public static class BehaviourTreeStats
    {
        // Number of nodes in the tree, the root included.
        public static int CountNodes(Node root)
        {
            if (root == null)
            {
                return 0;
            }

            int count = 1;
            foreach (var child in root.Children)
            {
                count += CountNodes(child);
            }

            return count;
        }
    }
}
