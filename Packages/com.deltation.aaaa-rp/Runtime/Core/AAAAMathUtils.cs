namespace DELTation.AAAARP.Core
{
    public static class AAAAMathUtils
    {
        public static int AlignUp(int value, int alignment)
        {
            if (alignment == 0)
            {
                return value;
            }
            return value + alignment - 1 & -alignment;
        }
    }
}