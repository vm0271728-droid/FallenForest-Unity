using FallenForest.Core;
using UnityEngine;
namespace FallenForest.Audio
{
    public sealed class AudioDirector : MonoBehaviour
    {
        [SerializeField] private AudioSource forestLoop, windLoop, horrorDrone, oneShotSource;
        [SerializeField] private Transform player;
        [SerializeField] private AudioClip[] randomForestEvents;
        [SerializeField] private Vector2 randomEventInterval = new(22f,68f), randomEventDistance = new(11f,34f);
        [SerializeField] private float maxDroneVolume=.28f, normalWindVolume=.40f, gustWindVolume=.70f, gustChancePerSecond=.014f;
        private float nextEventTime,gustTimer,gustTarget; private bool muted;
        private void Start(){if(player==null){FallenForest.Player.PlayerMotor m=FindFirstObjectByType<FallenForest.Player.PlayerMotor>();if(m!=null)player=m.transform;}if(forestLoop!=null&&!forestLoop.isPlaying)forestLoop.Play();if(windLoop!=null&&!windLoop.isPlaying)windLoop.Play();if(horrorDrone!=null&&!horrorDrone.isPlaying)horrorDrone.Play();ScheduleNextEvent();}
        private void Update(){if(muted)return;int docs=GameProgress.Instance!=null?GameProgress.Instance.DocumentsCollected:0;if(horrorDrone!=null){float p=Mathf.Clamp01(docs/10f),target=maxDroneVolume*p*p;horrorDrone.volume=Mathf.Lerp(horrorDrone.volume,target,1f-Mathf.Exp(-1.2f*Time.deltaTime));}if(Time.time>=nextEventTime){PlayRandomForestEvent();ScheduleNextEvent();}if(gustTimer<=0f&&Random.value<gustChancePerSecond*Time.deltaTime){gustTimer=Random.Range(2.5f,6f);gustTarget=Random.Range(.72f,1f);}else if(gustTimer>0f)gustTimer-=Time.deltaTime;float targetWind=gustTimer>0f?Mathf.Lerp(normalWindVolume,gustWindVolume,gustTarget):normalWindVolume;if(windLoop!=null)windLoop.volume=Mathf.Lerp(windLoop.volume,targetWind,1f-Mathf.Exp(-2f*Time.deltaTime));Shader.SetGlobalFloat("_FF_WindStrength",gustTimer>0f?Mathf.Lerp(.78f,1.38f,gustTarget):.55f);}
        private void ScheduleNextEvent(){float tension=GameProgress.Instance!=null?GameProgress.Instance.DocumentsCollected/10f:0f;nextEventTime=Time.time+Random.Range(randomEventInterval.x,randomEventInterval.y)*Mathf.Lerp(1f,.78f,tension);}
        private void PlayRandomForestEvent(){if(oneShotSource==null||randomForestEvents==null||randomForestEvents.Length==0)return;if(player!=null){float a=Random.Range(0f,Mathf.PI*2f),d=Random.Range(randomEventDistance.x,randomEventDistance.y);oneShotSource.transform.position=player.position+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*d+Vector3.up*Random.Range(.3f,2.1f);}oneShotSource.pitch=Random.Range(.92f,1.07f);oneShotSource.PlayOneShot(randomForestEvents[Random.Range(0,randomForestEvents.Length)],Random.Range(.24f,.48f));}
        public void HardSilence(){muted=true;if(forestLoop!=null)forestLoop.Stop();if(windLoop!=null)windLoop.Stop();if(horrorDrone!=null)horrorDrone.Stop();if(oneShotSource!=null)oneShotSource.Stop();Shader.SetGlobalFloat("_FF_WindStrength",0f);}
    }
}
