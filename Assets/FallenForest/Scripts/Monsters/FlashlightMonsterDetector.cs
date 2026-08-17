using FallenForest.Player;
using UnityEngine;
namespace FallenForest.Monsters
{
    public sealed class FlashlightMonsterDetector : MonoBehaviour
    {
        [SerializeField] private FlashlightController flashlight; [SerializeField] private float checkInterval=.065f,distance=60f,halfAngle=28f; [SerializeField] private LayerMask occlusionMask=~0; private float nextCheck;
        private void Update(){if(flashlight==null||!flashlight.Acquired||Time.time<nextCheck)return;nextCheck=Time.time+checkInterval;Ray ray=flashlight.CurrentRay;foreach(LocustAI l in FindObjectsByType<LocustAI>(FindObjectsSortMode.None))if(l!=null&&Illuminates(ray,l.HeadBone.position))l.OnHitByFlashlightFromDistance();foreach(BoiledOneEncounter b in FindObjectsByType<BoiledOneEncounter>(FindObjectsSortMode.None))if(b!=null&&Illuminates(ray,b.transform.position+Vector3.up*1.8f))b.OnIlluminated();}
        private bool Illuminates(Ray ray,Vector3 target){Vector3 to=target-ray.origin;float d=to.magnitude;if(d<=.05f||d>distance||Vector3.Angle(ray.direction,to/d)>halfAngle)return false;if(Physics.Raycast(ray.origin,to/d,out RaycastHit hit,d+.15f,occlusionMask,QueryTriggerInteraction.Ignore)){Transform h=hit.collider.transform;if(!h.IsChildOf(transform.root)&&Vector3.Distance(hit.point,target)>1.2f&&h.GetComponentInParent<LocustAI>()==null&&h.GetComponentInParent<BoiledOneEncounter>()==null)return false;}return true;}
    }
}
