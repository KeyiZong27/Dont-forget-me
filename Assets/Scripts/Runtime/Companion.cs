using UnityEngine;

namespace DontForgetMe
{
    // What each of the people on our side does once the alley turns into a fight: one works on
    // the gate, one stands in the way and shoves, one guards the worker's back, one cannot do
    // more than crouch against the wall. Nobody here simply stands and waits.
    public sealed class Companion : MonoBehaviour
    {
        public enum Duty { None, Brawl, Guard, Cower, Work }

        public Duty Role { get; private set; }
        public ActorMotor Motor { get; private set; }
        // Hands full with something else (carrying, closing the gate): leave the legs alone.
        public bool Busy { get; set; }
        public float StruckAt { get; private set; } = -100f;
        // For the worker: in place and not being pulled at, so the job is actually moving forward.
        public bool Working { get; private set; }
        public bool Harassed { get; private set; }

        private Siege siege;
        private Vector3 post;
        private Vector3 focus;
        private float leash;
        private float cooldown;
        private float nextShove;
        private float contactAt = -1f;
        private bool settled;
        private bool wasHarassed;
        private float harassedAt;

        public void Configure(Siege owner, ActorMotor motor)
        {
            siege = owner;
            Motor = motor;
        }

        // "focus" is what the role faces when nothing else demands attention: the work for the
        // worker, the direction trouble comes from for everyone else.
        public void Assign(Duty role, Vector3 where, Vector3 facing, float reach = 0f, float rest = 2.4f)
        {
            CharacterAnimator body = Motor.Body;
            if (Role == Duty.Cower || Role == Duty.Work) body.Relax();
            body.Cover = false;
            Role = role;
            post = where;
            focus = facing;
            leash = reach;
            cooldown = rest;
            settled = false;
            Working = false;
            Harassed = false;
            // Someone braced at a job or wedged against a wall is hard to shift; the rest give way normally.
            Motor.Weight = role == Duty.Work || role == Duty.Cower ? 3f : 1f;
            if (role == Duty.None) return;
            Motor.Hurry = role == Duty.Cower;
            Motor.GoTo(where);
        }

        public void Struck(Hostile by)
        {
            StruckAt = Time.time;
            Vector3 away = transform.position - by.transform.position;
            away.y = 0;
            away = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;
            CharacterAnimator body = Motor.Body;
            if (Role == Duty.Cower || Role == Duty.Work)
            {
                // Pinned against the wall or bent over the job: they take it where they are.
                body.Flinch();
                return;
            }
            Motor.Throw(away * 2.6f, 0.45f);
            body.Recoil(away, 0.6f);
            nextShove = Mathf.Max(nextShove, Time.time + 0.7f);
        }

        private static Vector3 Flat(Vector3 value)
        {
            value.y = 0;
            return value;
        }

        private void Update()
        {
            DemoGame game = DemoGame.Instance;
            if (game == null || game.IsPaused || game.IsFailed || Busy || Role == Duty.None || Motor == null) return;
            CharacterAnimator body = Motor.Body;
            bool there = !Motor.HasDestination && Flat(post - transform.position).magnitude < 0.8f;
            // The shove lands when the arms are out.
            if (contactAt > 0 && Time.time >= contactAt)
            {
                contactAt = -1f;
                siege.ShoveFrom(transform.position, transform.forward, 1.6f, 65f, Role == Duty.Guard ? 0.6f : 0.75f, transform);
            }
            switch (Role)
            {
                case Duty.Cower:
                {
                    Hostile near = siege.Nearest(transform.position, 14f, false);
                    body.LookTarget = near != null ? near.transform : null;
                    if (there && !settled)
                    {
                        settled = true;
                        Motor.Hurry = false;
                        Motor.Facing = focus;
                        body.Stance("m_crouch_idle");
                        body.Cover = true;
                    }
                    break;
                }

                case Duty.Work:
                {
                    Hostile pest = siege.Nearest(transform.position, 1.6f, true);
                    Harassed = there && (pest != null || Time.time - StruckAt < 1.8f);
                    Working = there && !Harassed;
                    if (!there)
                    {
                        // Shouldered off the spot by the press of bodies: get back to it.
                        if (!Motor.HasDestination && !Motor.Reeling) Motor.GoTo(post);
                        settled = false;
                        break;
                    }
                    if (!settled || Harassed != wasHarassed)
                    {
                        settled = true;
                        wasHarassed = Harassed;
                        harassedAt = Time.time;
                        if (Harassed)
                        {
                            body.Stance("m_idle_nervous_01");
                            body.Flinch();
                        }
                        else
                        {
                            body.Stance("m_work_mid");
                            body.LookTarget = null;
                            Motor.Facing = focus;
                        }
                    }
                    if (Harassed && pest != null)
                    {
                        Motor.Facing = pest.transform.position;
                        body.LookTarget = pest.transform;
                        // Left to it long enough, he drops the hammer hand and heaves them off himself.
                        // It costs him the time, but the gate is never stopped for good.
                        if (Time.time - harassedAt > 1.6f && Time.time >= nextShove && !body.Acting)
                            Heave(body, pest.transform.position, 3.0f);
                    }
                    break;
                }

                default:
                    Fight(body, there);
                    break;
            }
        }

        private void Heave(CharacterAnimator body, Vector3 at, float rest)
        {
            Vector3 to = Flat(at - transform.position);
            Motor.Stop();
            Motor.Facing = at;
            if (to.sqrMagnitude > 0.0025f) transform.rotation = Quaternion.LookRotation(to);
            body.Shove(at + Vector3.up * 1.3f - to.normalized * 0.14f);
            contactAt = Time.time + 0.17f;
            nextShove = Time.time + rest;
        }

        // Step in front of whoever is closest to getting through, and shove when they are in reach.
        private void Fight(CharacterAnimator body, bool there)
        {
            Hostile threat = siege.ThreatNear(post, leash + 1.7f);
            Hostile watched = threat != null ? threat : siege.Nearest(transform.position, 16f, false);
            body.LookTarget = watched != null ? watched.transform : null;
            if (Motor.Reeling || body.Acting) return;

            if (threat == null)
            {
                if (!there) { if (!Motor.HasDestination) Motor.GoTo(post); }
                else Motor.Facing = watched != null ? watched.transform.position : focus;
                return;
            }

            Vector3 to = Flat(threat.transform.position - transform.position);
            float distance = to.magnitude;
            Vector3 between = Flat(post - threat.transform.position);
            Vector3 stand = threat.transform.position + (between.sqrMagnitude > 0.01f ? between.normalized : -to.normalized) * 1.0f;
            stand = post + Vector3.ClampMagnitude(Flat(stand - post), leash);
            if (Flat(stand - transform.position).magnitude > 0.3f) Motor.GoTo(stand);
            else
            {
                Motor.Stop();
                Motor.Facing = threat.transform.position;
            }

            if (distance < 1.4f && Time.time >= nextShove) Heave(body, threat.transform.position, cooldown * Random.Range(0.85f, 1.25f));
        }
    }
}
