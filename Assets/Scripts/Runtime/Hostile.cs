using UnityEngine;

namespace DontForgetMe
{
    // One of the people who no longer answer when called. They close in on whoever they have
    // fixed on, rear back, and throw themselves forward; a shove sends them reeling but never
    // puts them down for good.
    public sealed class Hostile : MonoBehaviour
    {
        public enum Mode { Idle, Arrive, Advance, Windup, Recover, Reel, Down, Pound }

        public ActorMotor Motor { get; private set; }
        public Mode State { get; private set; } = Mode.Idle;
        public Transform Target { get; set; }
        // Whoever shoved this one last is remembered for a few seconds.
        public Transform Grudge => Time.time < grudgeUntil ? grudge : null;
        public bool Dangerous => State == Mode.Advance || State == Mode.Windup || State == Mode.Recover;
        public float Pace { get; set; } = 1.7f;
        public float Windup { get; set; } = 0.65f;

        private const float Reach = 1.3f;
        private Siege siege;
        private Transform grudge;
        private float grudgeUntil;
        private float until;
        private float reelAt;
        private float reelPower;
        private Vector3 reelDirection;
        private float nextStrike;
        private Vector3 poundSpot;
        private Vector3 poundMark;
        private bool frontRow;
        private bool beating;

        public void Configure(Siege owner, ActorMotor motor)
        {
            siege = owner;
            Motor = motor;
            Motor.Speed = Pace;
        }

        // Walk a fixed route first (round the corner into the alley), then go for people.
        public void Arrive(params Vector3[] route)
        {
            State = Mode.Arrive;
            Motor.Speed = Pace;
            Motor.GoVia(route);
        }

        public void Advance()
        {
            if (State == Mode.Reel || State == Mode.Down) return;
            StopBeating();
            poundSpot = Vector3.zero;
            State = Mode.Advance;
            Motor.Speed = Pace;
        }

        // Go to the shut gate and beat on it. Those in the second row can only press forward.
        public void PoundAt(Vector3 spot, Vector3 mark, bool front)
        {
            poundSpot = spot;
            poundMark = mark;
            frontRow = front;
            if (State == Mode.Reel || State == Mode.Down) return;
            State = Mode.Pound;
            Motor.Speed = Pace;
            Motor.GoTo(spot);
        }

        public void Shoved(Vector3 direction, float power, Transform by)
        {
            direction.y = 0;
            if (direction.sqrMagnitude < 0.0001f) direction = -transform.forward;
            direction.Normalize();
            State = Mode.Reel;
            reelAt = Time.time;
            reelPower = power;
            reelDirection = direction;
            float seconds = Mathf.Lerp(0.6f, 1.25f, Mathf.Clamp01(power));
            Motor.Throw(direction * Mathf.Lerp(3.4f, 6.6f, Mathf.Clamp01(power)) * Mathf.Max(1f, power), seconds);
            Motor.Stop();
            Motor.Facing = transform.position - direction * 3f;
            StopBeating();
            Motor.Body.Menace = 0;
            Motor.Body.Recoil(direction, seconds);
            if (by != null)
            {
                grudge = by;
                // The one the player shoved comes for the player; a shove from one of the others
                // holds their attention only briefly before they turn back to what they were after.
                grudgeUntil = Time.time + (by == DemoGame.Instance.Player.transform ? 5f : 2.5f);
            }
        }

        private void Update()
        {
            DemoGame game = DemoGame.Instance;
            if (game == null || game.IsPaused || game.IsFailed || Motor == null) return;
            CharacterAnimator body = Motor.Body;
            float dt = Time.deltaTime;
            switch (State)
            {
                case Mode.Idle:
                    body.Menace = Mathf.MoveTowards(body.Menace, 0f, dt);
                    break;

                case Mode.Arrive:
                    if (!Motor.HasDestination) Advance();
                    break;

                case Mode.Advance:
                {
                    if (Target == null)
                    {
                        Motor.Stop();
                        break;
                    }
                    Vector3 to = Target.position - transform.position;
                    to.y = 0;
                    float distance = to.magnitude;
                    body.LookTarget = Target;
                    body.Menace = Mathf.MoveTowards(body.Menace, Mathf.InverseLerp(5.5f, 1.8f, distance), dt * 1.5f);
                    if (distance > Reach * 0.75f) Motor.GoTo(Target.position - to / Mathf.Max(distance, 0.01f) * 0.7f);
                    else
                    {
                        Motor.Stop();
                        Motor.Facing = Target.position;
                    }
                    if (distance < Reach && Time.time > nextStrike)
                    {
                        State = Mode.Windup;
                        until = Time.time + Windup;
                        Motor.Stop();
                        Motor.Facing = Target.position;
                        body.Lunge(Target, Windup);
                        siege.Growl(transform.position);
                    }
                    break;
                }

                case Mode.Windup:
                    if (Target != null) Motor.Facing = Target.position;
                    if (Time.time >= until)
                    {
                        if (Target != null)
                        {
                            Vector3 to = Target.position - transform.position;
                            to.y = 0;
                            // The blow only lands on someone still in front and within arm's length.
                            if (to.magnitude < Reach + 0.3f && Vector3.Angle(transform.forward, to) < 75f)
                                siege.Struck(Target, this);
                        }
                        Motor.Throw(transform.forward * 2.2f, 0f);
                        State = Mode.Recover;
                        until = Time.time + Random.Range(1.3f, 2.0f);
                    }
                    break;

                case Mode.Recover:
                    body.Menace = Mathf.MoveTowards(body.Menace, 0.4f, dt * 2f);
                    if (Time.time >= until)
                    {
                        nextStrike = Time.time + 0.2f;
                        State = Mode.Advance;
                    }
                    break;

                case Mode.Reel:
                {
                    float since = Time.time - reelAt;
                    if (since < 0.4f) siege.Topple(this, reelDirection, reelPower);
                    // Driven hard into a wall, or into the others: down on hands and knees for a while.
                    if (since > 0.08f && since < 0.6f && Motor.HitScenery && reelPower >= 0.8f)
                    {
                        State = Mode.Down;
                        until = Time.time + Random.Range(1.8f, 2.6f);
                        body.Stance("m_crouch_idle", until - Time.time);
                        siege.Thud(transform.position, 0.5f);
                        break;
                    }
                    if (!Motor.Reeling) Resume();
                    break;
                }

                case Mode.Down:
                    if (Time.time >= until) Resume();
                    break;

                case Mode.Pound:
                {
                    Vector3 to = poundSpot - transform.position;
                    to.y = 0;
                    if (Motor.HasDestination && to.magnitude > 0.45f)
                    {
                        StopBeating();
                        break;
                    }
                    Motor.Stop();
                    Motor.Facing = poundMark;
                    if (!frontRow) body.Menace = Mathf.MoveTowards(body.Menace, 0.9f, dt);
                    else if (!beating)
                    {
                        beating = true;
                        body.Menace = 0;
                        Vector3 mark = poundMark;
                        body.Pound(() => mark, () => siege.GateStruck(mark));
                    }
                    break;
                }
            }
        }

        private void StopBeating()
        {
            beating = false;
            Motor.Body.StopPounding();
        }

        private void Resume()
        {
            nextStrike = Time.time + 0.5f;
            if (poundSpot != Vector3.zero)
            {
                State = Mode.Pound;
                Motor.GoTo(poundSpot);
            }
            else
            {
                State = Mode.Advance;
                Motor.Speed = Pace;
            }
        }
    }
}
