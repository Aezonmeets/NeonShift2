using UnityEngine;
public class HighScoreManager:MonoBehaviour{public static HighScoreManager Instance{get;private set;}
void Awake(){if(Instance!=null){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);}
public void TrySubmitScore(GameMode m,int s){string k="HS_"+m;if(s>PlayerPrefs.GetInt(k,0)){PlayerPrefs.SetInt(k,s);PlayerPrefs.Save();}}
public int GetHighScore(GameMode m)=>PlayerPrefs.GetInt("HS_"+m,0);}
