using System;
using System.Collections.Generic;

// Tokens identify provider-session endpoints, never persistent physical units.
public readonly struct Idas3EndpointToken : IEquatable<Idas3EndpointToken>
{
    public Guid Session { get; }
    public long Number { get; }
    internal Idas3EndpointToken(Guid session,long number){Session=session;Number=number;}
    public bool Equals(Idas3EndpointToken other)=>Session==other.Session&&Number==other.Number;
    public override bool Equals(object obj)=>obj is Idas3EndpointToken other&&Equals(other);
    public override int GetHashCode()=>Session.GetHashCode()^Number.GetHashCode();
    public override string ToString()=>Session.ToString("N")+":"+Number;
}
public enum Idas3SampleValidity { Unavailable, Valid, Invalid, Unsupported }
public enum Idas3EndpointStatus { Disconnected, Ready, PartialSample, Disabled, BackendUnavailable, ReadError, SuppressedMirror, UnsupportedControls, Disposed }
public enum Idas3SnapshotKind { Initial, Poll, Invalidation, Stopped }
public enum Idas3EvidenceStatus { Reported, Missing, Unsupported }
public readonly struct Idas3IdentityField
{
    public string Name { get; }
    public string Value { get; }
    public string Source { get; }
    public Idas3EvidenceStatus Status { get; }
    public Idas3IdentityField(string name,string value,string source,Idas3EvidenceStatus status){Name=name;Value=value;Source=source;Status=status;}
}
public sealed class Idas3EndpointIdentity
{
    public string ResolutionStatus=>"Unresolved";
    public string Reason=>"Reported metadata and session handles do not establish physical identity or cross-backend association.";
    public IReadOnlyList<Idas3IdentityField> Fields { get; }
    public Idas3EndpointIdentity(Idas3IdentityField[] fields){Fields=Array.AsReadOnly((Idas3IdentityField[])fields.Clone());}
}
// Future adapter seam only: the caller must validate provenance and endpoint/generation
// scope before using an association. No implementation or automatic matching in M1-A2.
public interface IIdas3ValidatedIdentityEvidenceSource
{
    bool TryGetEvidence(Idas3EndpointToken endpoint,long connectionGeneration,
        out Idas3EndpointIdentity evidence,out string validationReference);
}
public readonly struct Idas3ControlSample
{
    public string Path { get; }
    public string Label { get; }
    public float Minimum { get; }
    public float Maximum { get; }
    public bool Button { get; }
    public Idas3SampleValidity Validity { get; }
    public float? Value { get; }
    internal Idas3ControlSample(Idas3ControllerControl control,Idas3SampleValidity validity){
        Path=control.path;Label=control.label;Minimum=control.minimum;Maximum=control.maximum;Button=control.button;
        Validity=validity;Value=validity==Idas3SampleValidity.Valid?(float?)control.value:null;
    }
}
public sealed class Idas3EndpointSnapshot
{
    public Idas3EndpointToken Token { get; }
    public long ConnectionGeneration { get; }
    public long SampleSequence { get; }
    public double? SampledAt { get; }
    public string Backend { get; }
    public string Name { get; }
    public Idas3EndpointStatus Status { get; }
    public string StatusDetail { get; }
    public Idas3EndpointIdentity Identity { get; }
    public IReadOnlyList<Idas3ControlSample> Controls { get; }
    public bool CanRead=>Status==Idas3EndpointStatus.Ready||Status==Idas3EndpointStatus.PartialSample;
    // Alias suppression is backend-family evidence only. Other aliases remain unresolved.
    public string AliasStatus=>Status==Idas3EndpointStatus.SuppressedMirror?"Unity XInput family suppressed; native slots authoritative; per-unit association unresolved":"Unresolved";
    internal Idas3EndpointSnapshot(Idas3EndpointToken token,long generation,long sampleSequence,double? sampledAt,string backend,string name,
        Idas3EndpointStatus status,string detail,Idas3EndpointIdentity identity,Idas3ControlSample[] ownedControls){
        Token=token;ConnectionGeneration=generation;SampleSequence=sampleSequence;SampledAt=sampledAt;Backend=backend;Name=name;Status=status;StatusDetail=detail;Identity=identity;
        Controls=Array.AsReadOnly(ownedControls);
    }
    public bool TryGetControl(string path,out Idas3ControlSample sample){
        // Index immutable collections: avoid allocating an interface enumerator per action/control lookup.
        for(int i=0;i<Controls.Count;++i){var control=Controls[i];if(string.Equals(control.Path,path,StringComparison.Ordinal)){sample=control;return CanRead&&sample.Validity==Idas3SampleValidity.Valid;}}
        sample=default;return false;
    }
}
public sealed class Idas3DeviceFrame
{
    public Guid Session { get; }
    public long Sequence { get; }
    public long InventoryGeneration { get; }
    public double StartedAt { get; }
    public double CompletedAt { get; }
    public Idas3SnapshotKind Kind { get; }
    public IReadOnlyList<Idas3EndpointSnapshot> Endpoints { get; }
    internal Idas3DeviceFrame(Guid session,long sequence,long inventory,double start,double end,Idas3SnapshotKind kind,Idas3EndpointSnapshot[] ownedEndpoints){
        Session=session;Sequence=sequence;InventoryGeneration=inventory;StartedAt=start;CompletedAt=end;Kind=kind;
        Endpoints=Array.AsReadOnly(ownedEndpoints);
    }
    public bool TryGetEndpoint(Idas3EndpointToken token,out Idas3EndpointSnapshot endpoint){
        for(int i=0;i<Endpoints.Count;++i){var item=Endpoints[i];if(item.Token.Equals(token)){endpoint=item;return true;}}
        endpoint=null;return false;
    }
}
