using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>Lifecycle of the assembly's deterministic vertical motion.</summary>
internal enum VerticalMotionPhase
{
    Neutral,
    Entering,
    Running,
    Exiting
}

/// <summary>
/// Rich domain model for the assembly's vertical degree of freedom. The phase and
/// oscillator angle are explicit simulation state: no wall clock is consulted, and
/// each fixed physics step produces the same position, velocity, and acceleration.
/// </summary>
internal sealed class VerticalMotion : DomainModel
{
    private const double TwoPi = 2d * Math.PI;

    private VerticalMotionPhase _phase;
    private double _phaseElapsedSeconds;
    private double _oscillatorPhaseRadians;
    private double _exitStartEnvelope;
    private double _exitStartEnvelopeRate;
    private double _exitStartEnvelopeAcceleration;
    private double _positionMeters;
    private double _velocityMetersPerSecond;
    private double _accelerationMetersPerSecondSquared;

    public VerticalMotion()
        : base(isNew: true)
    {
    }

    public VerticalMotionPhase Phase => _phase;

    public double PositionMeters => _positionMeters;

    public double VelocityMetersPerSecond => _velocityMetersPerSecond;

    public double AccelerationMetersPerSecondSquared => _accelerationMetersPerSecondSquared;

    public bool IsNeutral => _phase == VerticalMotionPhase.Neutral;

    /// <summary>Starts the smooth entry profile from the neutral pose.</summary>
    public void BeginRun()
    {
        if (!IsNeutral)
        {
            throw new DomainValidationException("Vertical motion must return to neutral before another run starts.");
        }

        _phaseElapsedSeconds = 0d;
        _oscillatorPhaseRadians = 0d;
        SetPhase(VerticalMotionPhase.Entering);
        SetKinematics(default);
    }

    /// <summary>
    /// Begins a smooth return from the current trajectory. Repeated stop requests
    /// leave the original transition untouched.
    /// </summary>
    public void BeginReturnToNeutral()
    {
        if (_phase is VerticalMotionPhase.Neutral or VerticalMotionPhase.Exiting)
        {
            return;
        }

        var currentEnvelope = CalculateEnvelope();
        _exitStartEnvelope = currentEnvelope.Value;
        _exitStartEnvelopeRate = currentEnvelope.Rate;
        _exitStartEnvelopeAcceleration = currentEnvelope.Acceleration;
        _phaseElapsedSeconds = 0d;
        SetPhase(VerticalMotionPhase.Exiting);
    }

    /// <summary>Advances the profile by the supplied simulation timestep.</summary>
    public void Advance(double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds <= 0d)
        {
            throw new DomainValidationException("The vertical-motion step must be finite and positive.");
        }

        if (IsNeutral)
        {
            return;
        }

        _oscillatorPhaseRadians = (_oscillatorPhaseRadians
            + (AngularFrequencyRadiansPerSecond * elapsedSeconds)) % TwoPi;
        _phaseElapsedSeconds += elapsedSeconds;

        if (_phase == VerticalMotionPhase.Entering
            && HasReachedTransitionEnd())
        {
            _phaseElapsedSeconds = 0d;
            SetPhase(VerticalMotionPhase.Running);
        }
        else if (_phase == VerticalMotionPhase.Exiting
            && HasReachedTransitionEnd())
        {
            _phaseElapsedSeconds = 0d;
            _oscillatorPhaseRadians = 0d;
            SetPhase(VerticalMotionPhase.Neutral);
            SetKinematics(default);
            return;
        }

        SetKinematics(CalculateKinematics());
    }

    private static double AngularFrequencyRadiansPerSecond =>
        TwoPi * RideParameters.VerticalMotionFrequencyHz;

    private bool HasReachedTransitionEnd() =>
        _phaseElapsedSeconds + (RideParameters.TimeStep.TotalSeconds * 0.5d)
        >= RideParameters.VerticalMotionTransitionSeconds;

    private void SetPhase(VerticalMotionPhase value)
    {
        if (_phase == value)
        {
            return;
        }

        _phase = value;
        MarkChanged();
    }

    private void SetKinematics(VerticalMotionKinematics value)
    {
        if (!double.IsFinite(value.PositionMeters)
            || !double.IsFinite(value.VelocityMetersPerSecond)
            || !double.IsFinite(value.AccelerationMetersPerSecondSquared))
        {
            throw new DomainValidationException("Vertical-motion kinematics must remain finite.");
        }

        _positionMeters = value.PositionMeters;
        _velocityMetersPerSecond = value.VelocityMetersPerSecond;
        _accelerationMetersPerSecondSquared = value.AccelerationMetersPerSecondSquared;
        MarkChanged();
    }

    private VerticalMotionKinematics CalculateKinematics()
    {
        var envelopeKinematics = CalculateEnvelope();
        var envelope = envelopeKinematics.Value;
        var envelopeRate = envelopeKinematics.Rate;
        var envelopeAcceleration = envelopeKinematics.Acceleration;
        var angularFrequency = AngularFrequencyRadiansPerSecond;
        var sine = Math.Sin(_oscillatorPhaseRadians);
        var cosine = Math.Cos(_oscillatorPhaseRadians);
        var amplitude = RideParameters.VerticalMotionAmplitudeMeters;

        return new VerticalMotionKinematics(
            amplitude * envelope * sine,
            amplitude * ((envelopeRate * sine) + (envelope * angularFrequency * cosine)),
            amplitude * ((envelopeAcceleration * sine)
                + (2d * envelopeRate * angularFrequency * cosine)
                - (envelope * angularFrequency * angularFrequency * sine)));
    }

    private EnvelopeKinematics CalculateEnvelope()
    {
        if (_phase == VerticalMotionPhase.Running)
        {
            return new EnvelopeKinematics(1d, 0d, 0d);
        }

        var transitionFraction = _phase switch
        {
            VerticalMotionPhase.Entering =>
                _phaseElapsedSeconds / RideParameters.VerticalMotionTransitionSeconds,
            _ => 1d
        };

        if (_phase == VerticalMotionPhase.Exiting)
        {
            return CalculateReturnEnvelope(transitionFraction);
        }

        var fractionSquared = transitionFraction * transitionFraction;
        var fractionCubed = fractionSquared * transitionFraction;
        var fractionFourth = fractionCubed * transitionFraction;
        var fractionFifth = fractionFourth * transitionFraction;
        var smootherstep = (10d * fractionCubed) - (15d * fractionFourth) + (6d * fractionFifth);
        var smootherstepRate = 30d * fractionSquared * (1d - transitionFraction) * (1d - transitionFraction);
        var smootherstepAcceleration =
            60d * transitionFraction * (1d - transitionFraction) * (1d - (2d * transitionFraction));
        var transitionSeconds = RideParameters.VerticalMotionTransitionSeconds;

        return new EnvelopeKinematics(
            smootherstep,
            smootherstepRate / transitionSeconds,
            smootherstepAcceleration / (transitionSeconds * transitionSeconds));
    }

    private EnvelopeKinematics CalculateReturnEnvelope(double transitionFraction)
    {
        var transitionSeconds = RideParameters.VerticalMotionTransitionSeconds;
        var c0 = _exitStartEnvelope;
        var c1 = _exitStartEnvelopeRate * transitionSeconds;
        var c2 = 0.5d * _exitStartEnvelopeAcceleration * transitionSeconds * transitionSeconds;
        var c3 = (-10d * c0) - (6d * c1) - (3d * c2);
        var c4 = (15d * c0) + (8d * c1) + (3d * c2);
        var c5 = (-6d * c0) - (3d * c1) - c2;
        var fractionSquared = transitionFraction * transitionFraction;
        var fractionCubed = fractionSquared * transitionFraction;
        var fractionFourth = fractionCubed * transitionFraction;
        var fractionFifth = fractionFourth * transitionFraction;

        var value = c0 + (c1 * transitionFraction) + (c2 * fractionSquared)
            + (c3 * fractionCubed) + (c4 * fractionFourth) + (c5 * fractionFifth);
        var firstDerivative = c1 + (2d * c2 * transitionFraction)
            + (3d * c3 * fractionSquared) + (4d * c4 * fractionCubed) + (5d * c5 * fractionFourth);
        var secondDerivative = (2d * c2) + (6d * c3 * transitionFraction)
            + (12d * c4 * fractionSquared) + (20d * c5 * fractionCubed);

        return new EnvelopeKinematics(
            value,
            firstDerivative / transitionSeconds,
            secondDerivative / (transitionSeconds * transitionSeconds));
    }

    private readonly record struct EnvelopeKinematics(double Value, double Rate, double Acceleration);

    private readonly record struct VerticalMotionKinematics(
        double PositionMeters,
        double VelocityMetersPerSecond,
        double AccelerationMetersPerSecondSquared);
}
