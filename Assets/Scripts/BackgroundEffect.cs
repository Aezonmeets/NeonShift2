using UnityEngine;
public class BackgroundEffect : MonoBehaviour
{
    const int N=14; LineRenderer[] h,v; float scroll,hue;
    void Start(){h=new LineRenderer[N];v=new LineRenderer[N];for(int i=0;i<N;i++){h[i]=LR("H"+i);v[i]=LR("V"+i);}}
    LineRenderer LR(string n){var go=new GameObject(n);go.transform.SetParent(transform);var lr=go.AddComponent<LineRenderer>();lr.material=new Material(Shader.Find("Sprites/Default"));lr.positionCount=2;lr.sortingOrder=-3;lr.useWorldSpace=true;lr.startWidth=lr.endWidth=0.012f;return lr;}
    void Update()
    {
        scroll=(scroll+Time.deltaTime*1.6f)%1.8f; hue=(hue+Time.deltaTime*0.04f)%1f;
        var cam=Camera.main; if(!cam||!TrackController.Instance) return;
        float ang=TrackController.Instance.CurrentAngle;
        float pr=(ang+90)*Mathf.Deg2Rad,fr=ang*Mathf.Deg2Rad;
        Vector2 p=new Vector2(Mathf.Cos(pr),Mathf.Sin(pr)),f=new Vector2(Mathf.Sin(fr),-Mathf.Cos(fr));
        float hw=cam.orthographicSize*cam.aspect+3f,hh=cam.orthographicSize+3f;
        float sp=1.8f,span=N*sp;
        for(int i=0;i<N;i++)
        {
            bool ac=i%4==0; float alpha=ac?0.38f*(0.5f+Mathf.Sin(Time.time*2+i)*0.25f):0.12f;
            Color c=Color.HSVToRGB(hue,0.75f,0.7f); c.a=alpha; float lw=ac?0.028f:0.01f;
            float ho=-span/2+i*sp+scroll; Vector2 hc=p*ho;
            h[i].SetPosition(0,new Vector3(hc.x-f.x*hh,hc.y-f.y*hh,0));h[i].SetPosition(1,new Vector3(hc.x+f.x*hh,hc.y+f.y*hh,0));
            h[i].startColor=h[i].endColor=c;h[i].startWidth=h[i].endWidth=lw;
            float vo=-span/2+i*sp; Vector2 vc=f*vo; c.a=ac?0.25f:0.08f;
            v[i].SetPosition(0,new Vector3(vc.x-p.x*hw,vc.y-p.y*hw,0));v[i].SetPosition(1,new Vector3(vc.x+p.x*hw,vc.y+p.y*hw,0));
            v[i].startColor=v[i].endColor=c;v[i].startWidth=v[i].endWidth=lw;
        }
    }
}
