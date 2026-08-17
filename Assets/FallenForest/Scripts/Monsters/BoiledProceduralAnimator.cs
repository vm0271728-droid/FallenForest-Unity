using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallenForest.Monsters
{
    /// <summary>
    /// Skeletal fallback for the supplied Boiled One rig. It deliberately avoids a normal humanoid
    /// walk cycle: the Body1..Body5 chain bends asynchronously while the eye jiggle bones twitch.
    /// Head orientation remains owned by BoiledOneEncounter's gaze tracking.
    /// </summary>
    public sealed class BoiledProceduralAnimator : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float bodyAmplitude = 2.4f;
        [SerializeField] private float bodyFrequency = .37f;
        [SerializeField] private float eyeJitterDegrees = 1.8f;

        private readonly List<Transform> body = new();
        private readonly List<Quaternion> bodyBind = new();
        private readonly List<Transform> eyes = new();
        private readonly List<Quaternion> eyeBind = new();
        private float seed;
        private bool ready;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                enabled = false;
                return;
            }
            if (animator != null) animator.enabled = false;

            ResolveRig();
            seed = UnityEngine.Random.Range(0f, 100f);
        }

        private void LateUpdate()
        {
            if (!ready) return;
            float t = Time.unscaledTime + seed;

            for (int i = 0; i < body.Count; i++)
            {
                Transform bone = body[i];
                float phase = t * bodyFrequency * (1f + i * .09f) + i * 1.37f;
                float x = Mathf.Sin(phase * .73f) * bodyAmplitude * (.35f + i * .10f);
                float y = Mathf.Sin(phase * .47f + 1.1f) * bodyAmplitude * (.22f + i * .07f);
                float z = Mathf.Sin(phase + .4f) * bodyAmplitude * (.52f + i * .09f);
                bone.localRotation = bodyBind[i] * Quaternion.Euler(x, y, z);
            }

            for (int i = 0; i < eyes.Count; i++)
            {
                Transform eye = eyes[i];
                float irregular = Mathf.PerlinNoise(seed + i * 2.13f, t * (1.9f + i * .17f)) * 2f - 1f;
                float pulse = Mathf.Sin(t * (4.1f + i * .31f) + i) * .35f;
                eye.localRotation = eyeBind[i] * Quaternion.Euler(
                    irregular * eyeJitterDegrees,
                    pulse * eyeJitterDegrees,
                    -irregular * eyeJitterDegrees * .55f);
            }
        }

        private void ResolveRig()
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int index = 1; index <= 5; index++)
            {
                Transform found = FindExact(all, "Body" + index);
                if (found != null)
                {
                    body.Add(found);
                    bodyBind.Add(found.localRotation);
                }
            }

            foreach (Transform candidate in all)
            {
                if (!candidate.name.StartsWith("JiggleEye", StringComparison.OrdinalIgnoreCase)) continue;
                eyes.Add(candidate);
                eyeBind.Add(candidate.localRotation);
            }

            ready = body.Count > 0;
            if (!ready)
                Debug.LogWarning("Fallen Forest: Boiled procedural animator could not resolve Body1..Body5 rig bones.", this);
        }

        private static Transform FindExact(Transform[] all, string name)
        {
            foreach (Transform candidate in all)
                if (string.Equals(candidate.name, name, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            return null;
        }
    }
}
