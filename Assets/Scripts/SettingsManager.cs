using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    [Header("UI Sliders")]
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Optional")]
    [Tooltip("Drag your Main Menu music AudioSource here so it updates in real-time!")]
    public AudioSource mainMenuMusic; 

    void Start()
    {
        // 1. Load the saved volumes (using the exact same keys as GameManager)
        float savedMusic = PlayerPrefs.GetFloat("MusicVolume", 0.55f);
        float savedSFX = PlayerPrefs.GetFloat("SFXVolume", 0.8f);

        // 2. Set the sliders to match the saved values
        if (musicSlider != null) musicSlider.value = savedMusic;
        if (sfxSlider != null) sfxSlider.value = savedSFX;

        // 3. Make the sliders trigger the save functions when you drag them
        if (musicSlider != null) 
            musicSlider.onValueChanged.AddListener(UpdateMusicVolume);
        
        if (sfxSlider != null) 
            sfxSlider.onValueChanged.AddListener(UpdateSFXVolume);
    }

    public void UpdateMusicVolume(float volume)
    {
        // Save the new music volume
        PlayerPrefs.SetFloat("MusicVolume", volume);
        PlayerPrefs.Save();

        // Update main menu music immediately if you have it attached
        if (mainMenuMusic != null) mainMenuMusic.volume = volume;
    }

    public void UpdateSFXVolume(float volume)
    {
        // Save the new SFX volume
        PlayerPrefs.SetFloat("SFXVolume", volume);
        PlayerPrefs.Save();
    }
}