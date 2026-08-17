using System.Collections;
using FallenForest.Player;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
namespace FallenForest.Cinematics
{
    public sealed class BoiledOneSequence : MonoBehaviour
    {
        [SerializeField] private VideoPlayer videoPlayer; [SerializeField] private RawImage videoSurface; [SerializeField] private CanvasGroup videoGroup, blackout; [SerializeField] private WakeUpSequence wakeUp; [SerializeField] private PlayerMotor playerMotor; [SerializeField] private CameraMotion cameraMotion; [SerializeField] private float postVideoBlack=.45f; private bool playing;
        public IEnumerator PlayAtCurrentPlayerPosition(Transform player)
        {
            if(playing)yield break;playing=true;if(playerMotor==null)playerMotor=FindFirstObjectByType<PlayerMotor>();if(cameraMotion==null)cameraMotion=FindFirstObjectByType<CameraMotion>();if(wakeUp==null)wakeUp=FindFirstObjectByType<WakeUpSequence>();Vector3 wakePosition=player!=null?player.position:Vector3.zero;Quaternion wakeRotation=player!=null?player.rotation:Quaternion.identity;playerMotor?.SetControlsEnabled(false);cameraMotion?.SetInputEnabled(false);if(videoGroup!=null)videoGroup.alpha=1f;if(videoSurface!=null)videoSurface.gameObject.SetActive(true);
            if(videoPlayer!=null){videoPlayer.Prepare();float timeout=5f;while(!videoPlayer.isPrepared&&timeout>0f){timeout-=Time.unscaledDeltaTime;yield return null;}videoPlayer.Play();while(videoPlayer.isPlaying)yield return null;}else yield return new WaitForSecondsRealtime(2f);
            if(videoSurface!=null)videoSurface.gameObject.SetActive(false);if(videoGroup!=null)videoGroup.alpha=0f;if(blackout!=null)blackout.alpha=1f;yield return new WaitForSecondsRealtime(postVideoBlack);if(player!=null){player.position=wakePosition;player.rotation=wakeRotation;}playerMotor?.Teleport(wakePosition);if(wakeUp!=null)yield return wakeUp.PlayWakeUp(false);if(blackout!=null)blackout.alpha=0f;cameraMotion?.SetInputEnabled(true);playerMotor?.SetControlsEnabled(true);playing=false;
        }
    }
}
