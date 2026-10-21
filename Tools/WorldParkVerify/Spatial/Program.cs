using System;
class Program
{
    static int Main()
    {
        try
        {
            var tests = new WorldParkSpatialTests();
            tests.PlacementUsesBottomOriginAndRejectsBlockedOrUnreachableCells();
            tests.PointHitTestPreservesExistingListOrderForSameCell();
            Console.WriteLine("WorldPark spatial pure checks: 2/2 passed");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
