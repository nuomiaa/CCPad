namespace CCPad.Terminal
{
    public enum SplitOrientation
    {
        Horizontal, // top / bottom
        Vertical    // left / right
    }

    public enum Direction
    {
        Left, Right, Up, Down
    }

    public abstract class SplitNode
    {
        public SplitNode? Parent { get; set; }
    }

    public class PaneNode : SplitNode
    {
        public TabPanel Panel { get; set; } = null!;
    }

    public class SplitContainerNode : SplitNode
    {
        public SplitOrientation Orientation { get; set; }
        public SplitNode First { get; set; } = null!;   // left or top
        public SplitNode Second { get; set; } = null!;  // right or bottom
        public double SplitRatio { get; set; } = 0.5;
    }
}
