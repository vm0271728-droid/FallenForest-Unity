using System.Collections;
using FallenForest.Cinematics;
using FallenForest.Core;
using UnityEngine;
namespace FallenForest.Monsters
{
    public sealed class BoiledOneEncounter : MonoBehaviour
    {
        [SerializeField] private Animator animator; [SerializeField] private Transform visualRoot, headBone; [SerializeField] private float reactionDelay=.08f,kneelDuration=1.15f; [SerializeField] private Vector2 untriggeredLifetime=new(28f,42f); [SerializeField] private AudioClip preVideoSting; [SerializeField] private BoiledOneSequence sequence;
        private Transform player; private MonsterDirector director; private bool triggered; private float expireAt; private Vector3 visualStartLocalPosition; private Quaternion visualStartLocalRotation;
        public void BeginEncounter(Transform p, MonsterDirector owner){player=p;director=owner;GameProgress.Instance?.MarkBoiledEncountered();if(visualRoot==null)visualRoot=transform;visualStartLocalPosition=visualRoot.localPosition;visualStartLocalRotation=visualRoot.localRotation;FacePlayerOnce();animator?.SetTrigger("IdleStand");expireAt=Time.time+Random.Range(untriggeredLifetime.x,untriggeredLifetime.y);}
        private void Update(){if(triggered)return;TrackHeadVerySlowly();if(Time.time>=expireAt){director?.NotifyEncounterFinished();Destroy(gameObject);}}
        private void FacePlayerOnce(){if(player==null)return;Vector3 flat=Vector3.ProjectOnPlane(player.position-transform.position,Vector3.up);if(flat.sqrMagnitude>.001f)transform.rotation=Quaternion.LookRotation(flat.normalized);}
        private void TrackHeadVerySlowly(){if(headBone==null||player==null)return;Vector3 d=player.position+Vector3.up*1.4f-headBone.position;if(d.sqrMagnitude<.001f)return;headBone.rotation=Quaternion.Slerp(headBone.rotation,Quaternion.LookRotation(d.normalized,Vector3.up),1f-Mathf.Exp(-.7f*Time.deltaTime));}
        public void OnIlluminated(){if(triggered||GameProgress.Instance==null)return;triggered=true;StartCoroutine(ReactionRoutine());}
        private IEnumerator ReactionRoutine(){yield return new WaitForSeconds(reactionDelay);if(preVideoSting!=null)AudioSource.PlayClipAtPoint(preVideoSting,transform.position,1f);if(animator!=null){animator.SetTrigger("Kneel");animator.SetBool("EyesClosed",true);}SetEyeCloseBlendShapes(100f);yield return AnimateKneelFallback();foreach(Renderer r in GetComponentsInChildren<Renderer>())r.enabled=false;foreach(Collider c in GetComponentsInChildren<Collider>())c.enabled=false;if(sequence==null)sequence=FindFirstObjectByType<BoiledOneSequence>();if(sequence!=null)yield return sequence.PlayAtCurrentPlayerPosition(player);director?.NotifyEncounterFinished();Destroy(gameObject);}
        private IEnumerator AnimateKneelFallback(){if(visualRoot==null){yield return new WaitForSeconds(kneelDuration);yield break;}Vector3 start=visualStartLocalPosition,end=start+Vector3.down*.63f;Quaternion sr=visualStartLocalRotation,er=sr*Quaternion.Euler(11f,0,0);Quaternion hr=headBone!=null?headBone.localRotation:Quaternion.identity,he=hr*Quaternion.Euler(24f,0,0);float t=0f;while(t<kneelDuration){t+=Time.deltaTime;float u=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/Mathf.Max(.01f,kneelDuration)));visualRoot.localPosition=Vector3.Lerp(start,end,u);visualRoot.localRotation=Quaternion.Slerp(sr,er,u);if(headBone!=null)headBone.localRotation=Quaternion.Slerp(hr,he,u);yield return null;}}
        private void SetEyeCloseBlendShapes(float value){foreach(SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true)){Mesh m=smr.sharedMesh;if(m==null)continue;for(int i=0;i<m.blendShapeCount;i++){string n=m.GetBlendShapeName(i).ToLowerInvariant().Replace("_","").Replace(" ","");if(n.Contains("blink")||n.Contains("eyeclose")||n.Contains("eyesclose")||n.Contains("closeeye"))smr.SetBlendShapeWeight(i,value);}}}
    }
}
