using System.Collections.Generic;

namespace SDataLinqProvider
{
    public interface ISDataClientEntity
    {
        SDataEntityRepository Repository { get; set; }
        string ETag { get; set; }
        Dictionary<string, string> ForeignKeys { get; set; }
    }
}