using System.Text;
using FellowOakDicom;
using FellowOakDicom.Network;
using JetBrains.Annotations;

namespace PacsRefetch;
[UsedImplicitly]
public class CStoreProvider : DicomService, IDicomServiceProvider, IDicomCEchoProvider, IDicomCStoreProvider
{
    internal static Options? o;

    private static readonly DicomTransferSyntax[] ts = {
        DicomTransferSyntax.ExplicitVRLittleEndian,
        DicomTransferSyntax.ExplicitVRBigEndian,
        DicomTransferSyntax.ImplicitVRLittleEndian
    };

    /// <summary>
    /// File Transfer Syntax list
    /// List of acceptable transfer syntax names, lossless first
    /// </summary>
    internal static DicomTransferSyntax[] fts= DicomTransferSyntax.KnownEntries.OrderBy(e => e.IsLossy).ToArray();
    
    public CStoreProvider(INetworkStream stream, Encoding fallbackEncoding, Microsoft.Extensions.Logging.ILogger logger, DicomServiceDependencies dependencies) : base(stream, fallbackEncoding, logger, dependencies)
    {
    }

    public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
    {
        Console.Error.WriteLine($"Abort received from PACS, reason {reason}");
    }

    public void OnConnectionClosed(Exception exception)
    {
        Console.Error.WriteLine($"Connection closed: {exception}");
    }

    public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
    {
        ArgumentNullException.ThrowIfNull(o);
        Console.Error.WriteLine($"Association request received to '{association.CalledAE}' from '{association.CallingAE}'");
        if (association.CalledAE != o.Pacs.LocalName)
            return SendAssociationRejectAsync(DicomRejectResult.Permanent, DicomRejectSource.ServiceUser,
                DicomRejectReason.CalledAENotRecognized);
        if (association.CallingAE != o.Pacs.RemoteName)
            return SendAssociationRejectAsync(DicomRejectResult.Permanent, DicomRejectSource.ServiceUser,
                DicomRejectReason.CallingAENotRecognized);
        foreach (var pc in association.PresentationContexts)
        {
            if (pc.AbstractSyntax == DicomUID.Verification)
                pc.AcceptTransferSyntaxes(ts);
            else if (pc.AbstractSyntax.StorageCategory != DicomStorageCategory.None)
                pc.AcceptTransferSyntaxes(fts);
            return SendAssociationAcceptAsync(association);
        }

        return SendAssociationAcceptAsync(association);
    }

    public Task OnReceiveAssociationReleaseRequestAsync()
    {
        Console.Error.WriteLine("Release request received");
        return SendAssociationReleaseResponseAsync();
    }

    public Task<DicomCEchoResponse> OnCEchoRequestAsync(DicomCEchoRequest request)
    {
        return Task.FromResult(new DicomCEchoResponse(request, DicomStatus.Success));
    }

    public async Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request)
    {
        await request.File.SaveAsync($"{request.SOPInstanceUID.UID}.dcm");
        return new DicomCStoreResponse(request, DicomStatus.Success);
    }

    public async Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e)
    {
        await Console.Error.WriteLineAsync($"CStoreRequestException {e} on {tempFileName}");
    }
}