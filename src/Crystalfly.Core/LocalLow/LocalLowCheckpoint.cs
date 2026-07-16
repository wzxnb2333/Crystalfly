namespace Crystalfly.Core.LocalLow;

public enum LocalLowCheckpoint
{
    TakeoverBackupStaged,
    TakeoverBackupCommitted,
    ActivationStaged,
    SharedPreserved,
    InstanceActivated,
    CaptureStaged,
    InstancePreserved,
    InstanceCaptured,
    ActiveSharedRemoved,
    SharedRestored
}
