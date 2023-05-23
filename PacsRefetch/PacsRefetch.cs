using System.Collections.Concurrent;
using CommandLine;
using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using PacsRefetch;

Console.CancelKeyPress += (_, e) =>
{
    Options.cts.Cancel();
    e.Cancel = true;    // Mark event as handled
};


static async void Run(Options o)
{
    var studyQueue = new List<string>();
    var partialStudies=new List<string>();
    var studyCount = new ConcurrentDictionary<string, uint>();
    var seriesCount = new ConcurrentDictionary<string, uint>();
    var instanceCount = new ConcurrentDictionary<string, uint>();

    CStoreProvider.o = o;
    var po = new ParallelOptions
    {
        CancellationToken = Options.cts.Token
    };
    
    // 1. Enumerate DICOM files in current directory (if any), storing study + series + instance UIDs
    await Parallel.ForEachAsync(Directory.EnumerateFiles("."), po,async (path, ct) =>
    {
        try
        {
            if (!DicomFile.HasValidHeader(path))
                return;
            var dicomFile = await DicomFile.OpenAsync(path);
            ct.ThrowIfCancellationRequested();
            var ds = dicomFile.Dataset;
            var study = ds.GetString(DicomTag.StudyInstanceUID);
            studyCount.AddOrUpdate(study, 1, (_, v) => v + 1);
            var series = ds.GetString(DicomTag.SeriesInstanceUID);
            seriesCount.AddOrUpdate(series, 1, (_, v) => v + 1);
            var instance = ds.GetString(DicomTag.SOPInstanceUID);
            instanceCount.AddOrUpdate(instance, 1, (_, v) => v + 1);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Ignoring '{path} due to {e}'");
        }
    });
    
    // 2. Connect to PACS
    var client = DicomClientFactory.Create(o.Pacs.Hostname, o.Pacs.RemotePort, o.Pacs.UseTls, o.Pacs.LocalName, o.Pacs.RemoteName);
    // Also fire up a StoreSCP for the incoming studies to land in
    using var server = DicomServerFactory.Create<CStoreProvider>(o.Pacs.LocalPort);

    // 3. Ask the PACS which studies are in scope
    var studyList = DicomCFindRequest.CreateStudyQuery(o.Patient,null,o.DicomWindow);
    studyList.Dataset.AddOrUpdate(DicomTag.StudyInstanceUID, "");
    studyList.Dataset.AddOrUpdate(DicomTag.NumberOfStudyRelatedInstances, "");

    async void OnStudyListOnResponseReceived(DicomCFindRequest req, DicomCFindResponse res)
    {
        var ds = res.Dataset;
        if (res.Status != DicomStatus.Success || ds == null) throw new ApplicationException($"Error or null Dataset received in CFind response {res} (status {res.Status}) to {req}");
        var study = ds.GetString(DicomTag.StudyInstanceUID);

        // Three possibilities: fully fetched, not fetched at all, partially fetched.
        // Or the strange case, if some entries have been expired so we now have
        // *more* instances for this study than the PACS does!
        if (!studyCount.TryGetValue(study, out var ourInstances))
            studyQueue.Add(study);
        else
        {
            var theirInstances = ds.GetSingleValue<int>(DicomTag.NumberOfStudyRelatedInstances);
            if (ourInstances < theirInstances)
                partialStudies.Add(study);
            else if (ourInstances > theirInstances) await Console.Out.WriteLineAsync("Disappearing instances detected");
        }
    }

    studyList.OnResponseReceived += OnStudyListOnResponseReceived;
    await client.AddRequestAsync(studyList);
    await client.SendAsync();

    await Console.Out.WriteLineAsync($"Found {studyQueue.Count} full studies and {partialStudies.Count} partial.");


    // 4. Fill gaps in the partially-fetched studies
    partialStudies.Each(s => {
        var seriesList = DicomCFindRequest.CreateSeriesQuery(s);
        seriesList.Dataset.AddOrUpdate(DicomTag.NumberOfSeriesRelatedInstances,"");
        seriesList.OnResponseReceived += async (req,res) => {
            var ds = res.Dataset;
            if (res.Status != DicomStatus.Success || ds == null)
                throw new ApplicationException($"Error or null Dataset received in CFind response {res} (status {res.Status}) to {req}");
            var study = ds.GetString(DicomTag.SeriesInstanceUID);
        };
    });
    
    // 5. Fetch the full studies needed
    await client.AddRequestsAsync(studyQueue.Select(study => new DicomCMoveRequest(o.Pacs.LocalName, study, DicomPriority.Low)));
    await client.SendAsync();
}

static void Errors(IEnumerable<Error> e)
{
    Console.Error.WriteLine($"{e}");
}

Parser.Default.ParseArguments<Options>(args).WithParsed(Run).WithNotParsed(Errors);