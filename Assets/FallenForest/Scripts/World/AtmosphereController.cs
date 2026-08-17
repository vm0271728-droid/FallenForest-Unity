using UnityEngine;
namespace FallenForest.World
{
    public sealed class AtmosphereController:MonoBehaviour
    {
        [SerializeField]private Camera targetCamera;[SerializeField]private Color fogColor=new(0.002f,0.003f,0.004f,1f);[SerializeField]private float fogDensity=.028f;[SerializeField]private Color ambientColor=new(.003f,.004f,.005f,1f);
        private void Awake(){RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=fogColor;RenderSettings.fogDensity=fogDensity;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=ambientColor;if(targetCamera==null)targetCamera=Camera.main;if(targetCamera!=null){targetCamera.clearFlags=CameraClearFlags.SolidColor;targetCamera.backgroundColor=Color.black;targetCamera.allowHDR=true;}}
    }
}
