namespace GameSwap.Functions.Storage;

public static class SchedulingRules
{
    public static bool Overlaps(int startMinutes, int endMinutes, int otherStartMinutes, int otherEndMinutes)
        => TimeUtil.Overlaps(startMinutes, endMinutes, otherStartMinutes, otherEndMinutes);
}
