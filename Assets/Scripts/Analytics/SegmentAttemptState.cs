using System;

public sealed class SegmentAttemptState
{
    public string AttemptId { get; private set; }
    public string SegmentId { get; private set; }
    public string SegmentVersion { get; private set; }
    public double ActiveSeconds { get; private set; }

    private string geometryHash;

    public bool Begin(string segmentId, string version, string capturedGeometryHash = "unknown")
    {
        if (AttemptId != null || string.IsNullOrWhiteSpace(segmentId) || string.IsNullOrWhiteSpace(version)) return false;
        AttemptId = Guid.NewGuid().ToString("N");
        SegmentId = segmentId;
        SegmentVersion = version;
        geometryHash = capturedGeometryHash;
        ActiveSeconds = 0;
        return true;
    }

    public void Advance(double seconds)
    {
        if (AttemptId != null && seconds >= 0 && !double.IsInfinity(seconds) && !double.IsNaN(seconds)) ActiveSeconds += seconds;
    }

    public CompletedSegmentAttempt End(string outcome)
    {
        if (outcome != "failure" && outcome != "success" && outcome != "unresolved") throw new ArgumentException(nameof(outcome));
        if (AttemptId == null) return null;
        var completed = new CompletedSegmentAttempt(AttemptId, SegmentId, SegmentVersion, geometryHash, ActiveSeconds);
        AttemptId = null;
        return completed;
    }
}

public sealed class CompletedSegmentAttempt
{
    public string AttemptId { get; }
    public string SegmentId { get; }
    public string SegmentVersion { get; }
    public string GeometryHash { get; }
    public double ActiveSeconds { get; }

    public CompletedSegmentAttempt(string attemptId, string segmentId, string segmentVersion,
        string geometryHash, double activeSeconds)
    {
        AttemptId = attemptId;
        SegmentId = segmentId;
        SegmentVersion = segmentVersion;
        GeometryHash = geometryHash;
        ActiveSeconds = activeSeconds;
    }
}
