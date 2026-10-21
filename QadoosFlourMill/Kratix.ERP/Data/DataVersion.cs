namespace Kratix.ERP.Data;

// Business data changes only through BusinessWriteFilter (every non-GET request) and the demo reset; both bump this.
// A cached read (the dashboard) is reused only while the version it was built at is still current.
public static class DataVersion
{
    private static long _current;

    public static long Current => Interlocked.Read(ref _current);

    public static void Bump() => Interlocked.Increment(ref _current);
}
