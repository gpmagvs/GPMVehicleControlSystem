namespace GPMVehicleControlSystem.Models
{
    public static class Extensions
    {
        public static int ToInt(this bool value)
        {
            return value ? 1 : 0;
        }
        public static string ToSymbol(this bool value, string symbol_true, string sybol_false)
        {
            return value ? symbol_true : sybol_false;
        }
    }
}
