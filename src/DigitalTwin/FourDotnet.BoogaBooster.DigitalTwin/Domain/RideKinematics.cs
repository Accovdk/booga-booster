using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// The nested-frame kinematics (see <c>docs/01-physics-overview.md</c> and
/// <c>docs/05-forces-and-g-forces.md</c>): where a gondola's pivot sits in the
/// world given the mill and hub angles, and the horizontal centrifugal field it
/// feels there. Pure geometry — no state.
/// </summary>
internal static class RideKinematics
{
    private const double HalfPi = Math.PI / 2d;

    /// <summary>The fixed mount angle of arm <paramref name="index"/> (0–3): 0°, 90°, 180°, 270°.</summary>
    public static double MountAngle(int index) => index * HalfPi;

    /// <summary>World position of hub <paramref name="hubIndex"/>'s centre.</summary>
    public static PlanarVector HubCentre(double millAngle, int hubIndex)
        => PlanarVector.FromAngle(millAngle + MountAngle(hubIndex)) * RideParameters.MillArmLength;

    /// <summary>World position of gondola <paramref name="gondolaIndex"/>'s pivot on the given hub.</summary>
    public static PlanarVector PivotPosition(double millAngle, int hubIndex, double hubAngle, int gondolaIndex)
    {
        var hubCentre = HubCentre(millAngle, hubIndex);
        var armWorldAngle = millAngle + hubAngle + MountAngle(gondolaIndex);
        return hubCentre + (PlanarVector.FromAngle(armWorldAngle) * RideParameters.HubArmLength);
    }

    /// <summary>
    /// The outward horizontal acceleration field at a pivot — the vector sum of the
    /// mill and hub centripetal contributions, each pointing away from its own axis
    /// with magnitude <c>ω²·r</c>. This is what drives the passive swing and what a
    /// rider feels as horizontal specific force.
    /// </summary>
    public static PlanarVector OutwardField(
        PlanarVector pivot,
        PlanarVector hubCentre,
        double millOmega,
        double hubOmega)
    {
        var fromMill = pivot * (millOmega * millOmega);
        var fromHub = (pivot - hubCentre) * (hubOmega * hubOmega);
        return fromMill + fromHub;
    }

    /// <summary>World-space position of the mill's central axis.</summary>
    public static RideWorldPosition MillWorldPosition(double assemblyVerticalPositionMeters) =>
        new(0d, 0d, assemblyVerticalPositionMeters);

    /// <summary>World-space position of a hub, preserving its nested planar transform.</summary>
    public static RideWorldPosition HubWorldPosition(
        double millAngle,
        int hubIndex,
        double assemblyVerticalPositionMeters) =>
        WithAssemblyHeight(HubCentre(millAngle, hubIndex), assemblyVerticalPositionMeters);

    /// <summary>World-space position of a gondola pivot, preserving both planar rotations.</summary>
    public static RideWorldPosition GondolaPivotWorldPosition(
        double millAngle,
        int hubIndex,
        double hubAngle,
        int gondolaIndex,
        double assemblyVerticalPositionMeters) =>
        WithAssemblyHeight(
            PivotPosition(millAngle, hubIndex, hubAngle, gondolaIndex),
            assemblyVerticalPositionMeters);

    /// <summary>World-space position of one seat, with only the assembly height added.</summary>
    public static RideWorldPosition SeatWorldPosition(
        double millAngle,
        int hubIndex,
        double hubAngle,
        int gondolaIndex,
        double gondolaAngle,
        SeatPosition seat,
        double assemblyVerticalPositionMeters)
    {
        var pivot = PivotPosition(millAngle, hubIndex, hubAngle, gondolaIndex);
        var worldFacing = millAngle + hubAngle + MountAngle(gondolaIndex) + gondolaAngle;
        var lateralDirection = seat switch
        {
            SeatPosition.Left => 1d,
            SeatPosition.Right => -1d,
            _ => throw new ArgumentOutOfRangeException(nameof(seat))
        };
        var seatOffset = PlanarVector.FromAngle(worldFacing + HalfPi)
            * (lateralDirection * RideParameters.SeatLateralOffset);

        return WithAssemblyHeight(pivot + seatOffset, assemblyVerticalPositionMeters);
    }

    private static RideWorldPosition WithAssemblyHeight(
        PlanarVector planarPosition,
        double assemblyVerticalPositionMeters) =>
        new(planarPosition.X, planarPosition.Y, assemblyVerticalPositionMeters);
}

/// <summary>A world-space ride position in metres, with +Z as the vertical axis.</summary>
internal readonly record struct RideWorldPosition(double X, double Y, double Z);
