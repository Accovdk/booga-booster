using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>Where the assembly lift is in its lifecycle (see <c>docs/06-vertical-motion.md</c>).</summary>
public enum LiftPhase
{
    /// <summary>At rest at neutral height: position, velocity and acceleration are all zero.</summary>
    Neutral,

    /// <summary>Repeating the periodic rise-and-fall profile.</summary>
    Running,

    /// <summary>Gliding back to neutral, whether the run ended normally or was interrupted mid-cycle.</summary>
    Returning,
}

/// <summary>
/// The vertical degree of freedom of the whole ride assembly: height, velocity and
/// acceleration along the world vertical axis (up is positive) plus its lifecycle
/// <see cref="LiftPhase"/>. Advanced on the fixed physics step with closed-form
/// profiles, so the trajectory is a pure, drift-free function of the tick count —
/// no wall clock, no randomness (determinism rule).
/// </summary>
public sealed class AssemblyLift : DomainModel
{
    private const double TwoPi = 2d * Math.PI;

    private LiftPhase _phase = LiftPhase.Neutral;
    private double _height = RideParameters.LiftNeutralHeightMeters;
    private double _velocity;
    private double _acceleration;

    // The cosine phase angle: wrapped to [0, 2π) while running, advanced to 2π (the
    // bottom of the cycle) while returning.
    private double _cyclePhase;

    public AssemblyLift()
        : base(isNew: true)
    {
    }

    /// <summary>The lifecycle phase.</summary>
    public LiftPhase Phase => _phase;

    /// <summary>Height of the assembly above its neutral datum (m).</summary>
    public double Height => _height;

    /// <summary>Vertical velocity (m/s), up positive.</summary>
    public double Velocity => _velocity;

    /// <summary>Vertical coordinate acceleration (m/s²), up positive.</summary>
    public double Acceleration => _acceleration;

    /// <summary><c>true</c> when the lift is at rest at neutral height.</summary>
    public bool IsNeutral => _phase == LiftPhase.Neutral;

    /// <summary>
    /// Starts the periodic motion from neutral. Entering is continuous in position and
    /// velocity because the profile begins at the bottom of its cosine with zero speed.
    /// A lift that is already running or returning is left as it is.
    /// </summary>
    public void BeginRunning()
    {
        if (_phase != LiftPhase.Neutral)
        {
            return;
        }

        _phase = LiftPhase.Running;
        _cyclePhase = 0d;
        MarkChanged();
    }

    /// <summary>
    /// Ends the run by letting the cosine profile finish its current cycle: the lift
    /// keeps its height and velocity (no jump) and settles at the bottom of the cycle,
    /// which is the neutral height with zero velocity. This also holds when stopping
    /// interrupts a cycle mid-way, and the lift never dips below neutral. At most one
    /// <see cref="RideParameters.LiftPeriodSeconds"/> is needed. A lift that has not
    /// left neutral yet returns at once; a lift that is not running is left alone.
    /// </summary>
    public void BeginReturning()
    {
        if (_phase != LiftPhase.Running)
        {
            return;
        }

        if (_cyclePhase == 0d)
        {
            Settle();
            return;
        }

        _phase = LiftPhase.Returning;
        MarkChanged();
    }

    /// <summary>Advances the lift one fixed step of <paramref name="dt"/> seconds.</summary>
    public void Advance(double dt)
    {
        switch (_phase)
        {
            case LiftPhase.Running:
                _cyclePhase = (_cyclePhase + (RideParameters.LiftAngularFrequency * dt)) % TwoPi;
                ApplyProfile();
                break;
            case LiftPhase.Returning:
                var step = RideParameters.LiftAngularFrequency * dt;
                if (_cyclePhase + step >= TwoPi - (step / 2d))
                {
                    Settle();
                }
                else
                {
                    _cyclePhase += step;
                    ApplyProfile();
                }

                break;
        }
    }

    private void ApplyProfile()
    {
        var half = RideParameters.LiftStrokeMeters / 2d;
        var omega = RideParameters.LiftAngularFrequency;
        _height = RideParameters.LiftNeutralHeightMeters + (half * (1d - Math.Cos(_cyclePhase)));
        _velocity = half * omega * Math.Sin(_cyclePhase);
        _acceleration = half * omega * omega * Math.Cos(_cyclePhase);
        MarkChanged();
    }

    private void Settle()
    {
        _phase = LiftPhase.Neutral;
        _cyclePhase = 0d;
        _height = RideParameters.LiftNeutralHeightMeters;
        _velocity = 0d;
        _acceleration = 0d;
        MarkChanged();
    }
}
