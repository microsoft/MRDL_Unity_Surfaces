// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Extensions.SceneTransitions;
using UnityEngine;

namespace Microsoft.MRDL
{
    public class ContextualHandMenu : MonoBehaviour
    {
        private enum DisplayModeEnum
        {
            Opening,
            Open,
            Closing,
            Closed,
        }

        private enum TargetModeEnum
        {
            Closed,
            Open
        }

        public bool ActivatedOnce { get; private set; }
        public bool IsOpen { get { return displayMode != DisplayModeEnum.Closed; } }

        [SerializeField]
        private GameObject animationTarget = null;
        [SerializeField]
        private AnimationCurve openCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField]
        private AnimationCurve closeCurve = AnimationCurve.Linear(0, 1, 1, 0);
        [SerializeField]
        private float disableDistance = 0.25f;

        private DisplayModeEnum displayMode = DisplayModeEnum.Closed;
        private TargetModeEnum targetMode = TargetModeEnum.Closed;
        private float timeOpened;
        private float timeClosed;
        private float openDuration;
        private float closeDuration;

        private void Awake()
        {
            Keyframe[] keys = openCurve.keys;
            openDuration = keys[keys.Length - 1].time;

            keys = closeCurve.keys;
            closeDuration = keys[keys.Length - 1].time;

            animationTarget.gameObject.SetActive(false);
            displayMode = DisplayModeEnum.Closed;
        }

        public void RequestOpen()
        {
            targetMode = TargetModeEnum.Open;
        }

        public void RequestClose()
        {
            targetMode = TargetModeEnum.Closed;
        }

        public void OpenMenu()
        {
            switch (displayMode)
            {
                case DisplayModeEnum.Open:
                case DisplayModeEnum.Opening:
                case DisplayModeEnum.Closing:
                    return;

                default:
                    break;
            }

            displayMode = DisplayModeEnum.Opening;
            timeOpened = Time.time;
            //ActivatedOnce = true;
        }

        public void CloseMenu()
        {
            switch (displayMode)
            {
                case DisplayModeEnum.Closed:
                case DisplayModeEnum.Closing:
                case DisplayModeEnum.Opening:
                    return;

                default:
                    break;
            }

            displayMode = DisplayModeEnum.Closing;
            timeClosed = Time.time;
        }

        protected virtual bool DoesContextProhibitMenu()
        {
            bool contextProhibited = false;

            // See if we're close to the active surface
            if (FingerSurface.ActiveSurface != null)
            {
                float distToSurface = Vector3.Distance(FingerSurface.ActiveSurface.SurfacePosition, transform.position);
                if (distToSurface - FingerSurface.ActiveSurface.SurfaceRadius < disableDistance)
                {
                    contextProhibited = true;
                }
            }

            // See if we're doing a transition
            ISceneTransitionService transitionService;
            if (MixedRealityServiceRegistry.TryGetService<ISceneTransitionService>(out transitionService) && transitionService.TransitionInProgress)
            {
                contextProhibited = true;
            }

            return contextProhibited;
        }

        private void Update()
        {
            bool contextProhibited = DoesContextProhibitMenu();

            if (contextProhibited)
            {
                CloseMenu();
            }
            else
            {
                switch (targetMode)
                {
                    case TargetModeEnum.Open:
                        OpenMenu();
                        break;

                    case TargetModeEnum.Closed:
                        CloseMenu();
                        break;
                }
            }

            switch (displayMode)
            {
                case DisplayModeEnum.Closed:
                    animationTarget.SetActive(false);
                    break;

                case DisplayModeEnum.Open:
                    animationTarget.SetActive(true);
                    animationTarget.transform.localScale = Vector3.one;
                    break;

                case DisplayModeEnum.Opening:
                    animationTarget.SetActive(true);
                    float timeSinceOpened = Time.time - timeOpened;
                    animationTarget.transform.localScale = Vector3.one * openCurve.Evaluate(timeSinceOpened);
                    if (timeSinceOpened > openDuration)
                        displayMode = DisplayModeEnum.Open;
                    break;

                case DisplayModeEnum.Closing:
                    animationTarget.SetActive(true);
                    float timeSinceClosed = Time.time - timeClosed;
                    animationTarget.transform.localScale = Vector3.one * closeCurve.Evaluate(timeSinceClosed);
                    if (timeSinceClosed > closeDuration)
                        displayMode = DisplayModeEnum.Closed;
                    break;
            }
        }

        private void OnDisable()
        {
            animationTarget.gameObject.SetActive(false);
            displayMode = DisplayModeEnum.Closed;
        }
    }
}