using UnityEngine;using UnityEngine.UI;using TMPro;using UnityEngine.SceneManagement;using System.Collections;
public class MainMenuManager:MonoBehaviour
{
    void Start(){Camera.main.backgroundColor=new Color(0.025f,0.025f,0.09f);Camera.main.clearFlags=CameraClearFlags.SolidColor;Build();}
    void Build()
    {
        var cgo=new GameObject("C");var cv=cgo.AddComponent<Canvas>();cv.renderMode=RenderMode.ScreenSpaceOverlay;
        var sc=cgo.AddComponent<CanvasScaler>();sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;sc.referenceResolution=new Vector2(1920,1080);sc.matchWidthOrHeight=0.5f;cgo.AddComponent<GraphicRaycaster>();
        var title=T(cgo,"NEON SHIFT",100,new Vector2(0,280),new Color(0,0.9f,1f),FontStyles.Bold);
        T(cgo,"4-Lane Rhythm Game — Tiles rotate! Stay sharp.",38,new Vector2(0,175),new Color(0.6f,0.8f,1f,0.7f),FontStyles.Italic);
        // Lane colour preview
        string[]kl={"D","F","J","K"};Color[]lc={new Color(0,0.92f,1),new Color(0.2f,1,0.3f),new Color(1,0.92f,0.1f),new Color(1,0.15f,0.75f)};
        float[]kx={-225f,-75f,75f,225f};
        for(int i=0;i<4;i++){var kt=T(cgo,"["+kl[i]+"]",36,new Vector2(kx[i],100),lc[i],FontStyles.Bold);kt.outlineColor=lc[i];kt.outlineWidth=0.2f;}
        string[]ml={"EASY","MEDIUM","HARD","ENDLESS"};Color[]mc={new Color(0.2f,0.85f,1),new Color(0.3f,1,0.45f),new Color(1,0.25f,0.45f),new Color(1,0.75f,0.1f)};float[]yp={35f,-60f,-155f,-250f};
        for(int i=0;i<4;i++){int mi=i;Btn(cgo,ml[i],mc[i],new Vector2(0,yp[i]),()=>{PlayerPrefs.SetInt("SelectedMode",mi);SceneManager.LoadScene("GameScene");});}
        Btn(cgo,"QUIT",new Color(0.5f,0.5f,0.6f),new Vector2(0,-345),()=>Application.Quit());
        StartCoroutine(Pulse(title));
    }
    IEnumerator Pulse(TextMeshProUGUI t){while(true){if(t)t.color=Color.HSVToRGB((Time.time*0.15f)%1f,0.6f,1f);yield return null;}}
    TextMeshProUGUI T(GameObject p,string txt,int sz,Vector2 pos,Color col,FontStyles style){var go=new GameObject("T");go.transform.SetParent(p.transform,false);var rt=go.AddComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.anchoredPosition=pos;rt.sizeDelta=new Vector2(1000,130);var t=go.AddComponent<TextMeshProUGUI>();t.text=txt;t.fontSize=sz;t.fontStyle=style;t.alignment=TextAlignmentOptions.Center;t.color=col;return t;}
    void Btn(GameObject p,string lbl,Color col,Vector2 pos,UnityEngine.Events.UnityAction cb){var go=new GameObject("B");go.transform.SetParent(p.transform,false);var rt=go.AddComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.anchoredPosition=pos;rt.sizeDelta=new Vector2(420,74);var img=go.AddComponent<Image>();img.color=new Color(col.r*.22f,col.g*.22f,col.b*.22f,0.9f);var btn=go.AddComponent<Button>();btn.targetGraphic=img;btn.onClick.AddListener(cb);var tgo=new GameObject("L");tgo.transform.SetParent(go.transform,false);var trt=tgo.AddComponent<RectTransform>();trt.anchorMin=Vector2.zero;trt.anchorMax=Vector2.one;trt.offsetMin=trt.offsetMax=Vector2.zero;var tmp=tgo.AddComponent<TextMeshProUGUI>();tmp.text=lbl;tmp.fontSize=40;tmp.fontStyle=FontStyles.Bold;tmp.alignment=TextAlignmentOptions.Center;tmp.color=new Color(col.r,col.g,col.b,1);}
}
