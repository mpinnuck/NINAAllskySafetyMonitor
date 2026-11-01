using System.ComponentModel.Composition;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINAAllskySafetyMonitor.Core.Traceability;

namespace NINAAllskySafetyMonitor.Plugin.Nina;

/// <summary>Supplies the Allsky safety monitor device to NINA's safety monitor chooser.</summary>
[Export(typeof(IEquipmentProvider))]
[Requirement("REQ-01")]
public sealed class AllskySafetyMonitorProvider : IEquipmentProvider<ISafetyMonitor>
{
    private readonly IProfileService profileService;

    [ImportingConstructor]
    public AllskySafetyMonitorProvider(IProfileService profileService)
    {
        this.profileService = profileService;
    }

    public string Name => "Allsky Safety Monitor";

    public IList<ISafetyMonitor> GetEquipment() =>
        new List<ISafetyMonitor> { new AllskySafetyMonitorDevice(profileService) };
}
