using System.Globalization;
using CommandLine;
using FellowOakDicom;
using JetBrains.Annotations;

namespace PacsRefetch;

[UsedImplicitly]
internal class Options
{
    internal static CancellationTokenSource cts=new();

    public PacsParameters Pacs { get; private set; } = new();
    private static DateTime ParseDateTime(string s)
    {
        if (DateTime.TryParseExact(s, "yyyyMMddHH", CultureInfo.InvariantCulture, DateTimeStyles.None, out var r))
            return r;
        if (DateTime.TryParseExact(s, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out r))
            return r;
        throw new ArgumentException($"Unrecognised date/time string '{s}'");
    }

    [Option('i', "patient", Required = false, HelpText = "Patient ID to do a single-patient fetch")]
    public string? Patient { get; set; } = null;

    internal DicomDateRange DicomWindow = null!;
    [Option('r',"range",Required = true,HelpText = "Date/time range to search for; 1s subtracted from upper limit for convenience")]
    public string Window
    {
        get => DicomWindow.ToString();
        set
        {
            var bits = value.Split('-');
            DicomWindow = new DicomDateRange(ParseDateTime(bits[0]), ParseDateTime(bits[1]).Subtract(TimeSpan.FromSeconds(1)));
        }
    }

    [Option('y', "yaml", Required = true, HelpText = "YAML config file name")]
    public string Yaml
    {
        get => Pacs.ToString();
        set => Pacs = PacsParameters.Load(value);
    }
    
    [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
    public bool Verbose { get; set; }
}