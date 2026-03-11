using UnityEngine;
[DefaultExecutionOrder(-200)]
public class ScoreLoader:MonoBehaviour{void Awake(){var gm=FindFirstObjectByType<GameManager>();if(gm)gm.currentMode=(GameMode)Mathf.Clamp(PlayerPrefs.GetInt("SelectedMode",0),0,3);}}
