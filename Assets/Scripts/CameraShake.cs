using UnityEngine;using System.Collections;
public class CameraShake:MonoBehaviour{public static CameraShake Instance{get;private set;}Vector3 o;Coroutine co;
void Awake(){if(Instance!=null){Destroy(gameObject);return;}Instance=this;o=transform.localPosition;}
public void Shake(float d=0.2f,float m=0.12f){if(co!=null)StopCoroutine(co);co=StartCoroutine(S(d,m));}
IEnumerator S(float d,float m){float t=0;while(t<d){t+=Time.deltaTime;float k=1-Mathf.SmoothStep(0,1,t/d);transform.localPosition=o+new Vector3(Random.Range(-1f,1f)*m*k,Random.Range(-1f,1f)*m*k,0);yield return null;}transform.localPosition=o;}}
