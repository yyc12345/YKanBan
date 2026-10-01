namespace YKanBan.Tests;

[TestClass]
public class SmokeTests
{
    [TestMethod]
    public void ThirdPartyPackagesResolve()
    {
        Assert.IsNotNull(typeof(Pidgin.Parser).Assembly);
        Assert.IsNotNull(typeof(Newtonsoft.Json.JsonConvert).Assembly);
        Assert.IsNotNull(typeof(Microsoft.Data.Sqlite.SqliteConnection).Assembly);
    }
}
