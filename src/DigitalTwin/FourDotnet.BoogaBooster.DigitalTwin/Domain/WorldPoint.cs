namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// A point in the ride's world frame: <c>X</c>/<c>Y</c> in the horizontal plane and
/// <c>Z</c> vertical (up positive). Pairs the planar nested-rotation geometry with the
/// assembly height (see <c>docs/06-vertical-motion.md</c>).
/// </summary>
public readonly record struct WorldPoint(double X, double Y, double Z)
{
    /// <summary>Lifts a planar point to world space at height <paramref name="z"/>.</summary>
    public static WorldPoint FromPlanar(PlanarVector planar, double z) => new(planar.X, planar.Y, z);

    /// <summary>The horizontal projection of this point.</summary>
    public PlanarVector Planar => new(X, Y);

    public static WorldPoint operator -(WorldPoint a, WorldPoint b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}
