using UnityEngine;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// Procedural animation for the low-poly people: hips, knees, shoulders and elbows are real joints,
    /// so walking bends the knees and sitting folds the legs over the seat with the hands in the lap.
    /// </summary>
    public class HumanRig : MonoBehaviour
    {
        // rig units: 1.9 tall to the top of the hair at scale 1; hip joints at 0.95, knees 0.42 below
        // them, soles 0.53 below the knees
        public const float RigHeight = 1.9f, HipHeight = 0.95f, ThighLength = 0.42f, ShinLength = 0.53f, ThighRadius = 0.085f;
        // the rest of the joint layout: torso pivot, shoulders, elbows and the neck (where the head turns)
        public const float TorsoY = 0.97f, ShoulderY = 1.46f, ElbowY = 1.18f, NeckY = 1.53f;

        public Transform body, legL, legR, kneeL, kneeR, torso, armL, armR, elbowL, elbowR, head, mouth;
        [Tooltip("Eyelids that slide down over the painted eyes when blinking (close-up people only).")]
        public Transform lidL, lidR;
        [Tooltip("Shown while the person looks at their phone.")]
        public GameObject phoneProp;
        public float walkSpeed;
        public bool talking, thinking, sitting, typing, phone;
        [Tooltip("Carries a bag on the right shoulder: that arm rests a little out over the bag and hardly swings.")]
        public bool shoulderBag;
        [Tooltip("Top of the seat in metres above the floor this person stands on.")]
        public float seatHeight = 0.46f;
        public Transform lookTarget;

        float t, phase, mouthTarget = 0.02f, mouthTimer, blinkTimer, blinkLeft;
        float seed;

        void Awake()
        {
            seed = Random.value * 10f;
            blinkTimer = Random.Range(0.5f, 5f);
        }

        static Quaternion X(float deg, float z = 0) => Quaternion.Euler(deg, 0, z);

        // an upper arm swung forward (pitch) and out (roll) after turning about its own length (twist), so the
        // elbow bends the forearm in towards the middle instead of straight ahead
        static Quaternion Arm(float pitch, float roll, float twist) => Quaternion.Euler(pitch, 0, roll) * Quaternion.Euler(0, twist, 0);

        void Update() => Pose(Time.deltaTime);

        /// <summary>
        /// Poses the joints for the current state. Scene builders call it with 0 so people are saved
        /// already sitting or typing instead of popping into place on the first frame.
        /// </summary>
        public void Pose(float dt)
        {
            t += dt;
            float breathe = Mathf.Sin(t * 1.6f + seed);
            float scale = Mathf.Max(0.01f, transform.lossyScale.y);

            if (sitting)
            {
                // thighs rest on the seat (hips one thigh-radius above it) and the legs fold so the feet
                // land on the floor: thighs slope down when the seat is high for this person, shins reach
                // forward when it is low
                float hip = seatHeight / scale + ThighRadius;
                body.localPosition = new Vector3(0, hip - HipHeight, 0);
                float drop = hip - ShinLength;
                float thigh = Mathf.Asin(Mathf.Clamp(drop / ThighLength, 0f, 0.5f)) * Mathf.Rad2Deg;
                float shin = drop < 0 ? Mathf.Acos(Mathf.Clamp01(hip / ShinLength)) * Mathf.Rad2Deg : 3f;
                legL.localRotation = X(thigh - 90f, 5);
                legR.localRotation = X(thigh - 90f, -5);
                kneeL.localRotation = X(90f - thigh - shin);
                kneeR.localRotation = X(90f - thigh - shin - 4f);
                torso.localRotation = X(typing ? 0f : -4f);
            }
            else
            {
                phase += dt * Mathf.Max(walkSpeed, 0) * 5.8f;
                float amp = Mathf.Clamp01(walkSpeed / 1.2f);
                float swing = Mathf.Sin(phase) * 28f * amp;
                body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Cos(phase)) * 0.03f * amp, 0);
                legL.localRotation = X(swing);
                legR.localRotation = X(-swing);
                // the knee bends while that leg swings forward
                kneeL.localRotation = X(Mathf.Max(0, Mathf.Cos(phase)) * 38f * amp + 3f);
                kneeR.localRotation = X(Mathf.Max(0, -Mathf.Cos(phase)) * 38f * amp + 3f);
                torso.localRotation = X(2f * amp);
            }

            // arms
            if (typing && !talking)
            {
                // forearms level over a keyboard at desk height, fingers tapping
                armL.localRotation = X(-36 + Mathf.Sin(t * 13f) * 2f, 5);
                armR.localRotation = X(-36 + Mathf.Sin(t * 11f + 1) * 2f, -5);
                elbowL.localRotation = X(-50);
                elbowR.localRotation = X(-50);
            }
            else if (typing)
            {
                // talking to the customer: forearms resting on the desk edge with the hands together in front,
                // one of them gesturing
                float gesture = Mathf.Sin(t * 3.1f + seed) * 6f;
                armL.localRotation = Arm(-24, -3, 28);
                armR.localRotation = Arm(-26 + gesture, 3, -28);
                elbowL.localRotation = X(-64);
                elbowR.localRotation = X(-68 - gesture);
            }
            else if (phone)
            {
                // the phone in the right hand in front of the chest; the other hand rests in the lap (or hangs)
                armR.localRotation = X(-22, -12);
                elbowR.localRotation = X(-100);
                armL.localRotation = sitting ? X(-10 + breathe * 0.6f, 12) : X(breathe * 1.5f, -5);
                elbowL.localRotation = X(sitting ? -46 : -12);
            }
            else if (sitting)
            {
                // arms hang from the shoulders, hands resting on the thighs
                armL.localRotation = X(-10 + breathe * 0.6f, 12);
                armR.localRotation = X(-10 - breathe * 0.6f, -12);
                elbowL.localRotation = X(-46);
                elbowR.localRotation = X(-46);
            }
            else
            {
                float amp = Mathf.Clamp01(walkSpeed / 1.2f);
                float swing = -Mathf.Sin(phase) * 22f * amp;
                float gesture = talking ? Mathf.Sin(t * 3.1f + seed) * 9f : 0f;
                float swingR = shoulderBag ? swing * 0.3f : swing;
                armL.localRotation = X(swing + breathe * 1.5f, -5);
                armR.localRotation = X(-swingR - breathe * 1.5f + gesture * 0.5f, shoulderBag ? 10f : 5f);
                elbowL.localRotation = X(-10 - 18 * amp);
                elbowR.localRotation = X(-10 - 18 * amp + gesture - (talking ? 25 : 0));
            }
            torso.localScale = new Vector3(1, 1 + breathe * 0.01f, 1);

            // head: look at target, nod when talking, tilt when thinking
            var headRot = Quaternion.identity;
            if (lookTarget)
            {
                var local = torso.InverseTransformPoint(lookTarget.position) - head.localPosition;
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -70, 70);
                float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -25, 25);
                headRot = Quaternion.Euler(pitch, yaw, 0);
            }
            if (talking) headRot *= Quaternion.Euler(Mathf.Sin(t * 6.5f) * 3f, Mathf.Sin(t * 2.3f) * 3f, 0);
            if (thinking) headRot *= Quaternion.Euler(4 + Mathf.Sin(t * 1.8f) * 4f, 0, 9f);
            if (phone) headRot *= Quaternion.Euler(24, 0, 0);
            head.localRotation = Quaternion.Slerp(head.localRotation, headRot, dt * 6f);

            // mouth: opens and closes in syllable-sized steps while talking
            if (mouth)
            {
                if (talking)
                {
                    mouthTimer -= dt;
                    if (mouthTimer <= 0)
                    {
                        mouthTarget = Random.Range(0.12f, 0.95f);
                        mouthTimer = Random.Range(0.05f, 0.12f);
                    }
                }
                else mouthTarget = 0.02f;
                var s = mouth.localScale;
                s.y = Mathf.Lerp(s.y, mouthTarget, dt * 20f);
                mouth.localScale = s;
            }

            // blinking every few seconds
            if (lidL && lidR)
            {
                blinkTimer -= dt;
                if (blinkTimer <= 0)
                {
                    blinkLeft = 0.15f;
                    blinkTimer = Random.Range(2.2f, 6f);
                }
                float closed = 0;
                if (blinkLeft > 0)
                {
                    blinkLeft -= dt;
                    closed = Mathf.Sin(Mathf.Clamp01(1f - blinkLeft / 0.15f) * Mathf.PI);
                }
                if (phone) closed = Mathf.Max(closed, 0.25f); // eyes lowered to the screen
                var ls = new Vector3(1, Mathf.Max(0.02f, closed), 1);
                lidL.localScale = ls;
                lidR.localScale = ls;
            }
            if (phoneProp && phoneProp.activeSelf != phone) phoneProp.SetActive(phone);
        }
    }
}
