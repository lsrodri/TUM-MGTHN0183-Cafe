using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;

public class StateManagement : MonoBehaviour
{
    [Header("Configuration")]
    public string participantsCsvName = "Participants.csv";

    [Header("Audio References")]
    public AudioClip[] audioClips;

    [Header("Post Processing Volume")]
    public Volume postProcessingVolume;

    [Header("GameObjects to Toggle")]
    public GameObject menu;
    public GameObject food;
    public GameObject survey;


    [Header("Status (Read Only)")]
    [SerializeField] private int participantID;
    [SerializeField] private int condition;
    [SerializeField] private int currentPhase;

    // Public getters
    public int ParticipantID => participantID;
    public int Condition => condition;
    public int CurrentPhase => currentPhase;



    // Condition data structure
    [System.Serializable]
    private class ParticipantData
    {
        public int participantID;
        public int condition; // 1, 2, 3, or 4
    }

    private List<ParticipantData> allParticipants = new List<ParticipantData>();

    void Start()
    {
        // Get participant ID from PlayerPrefs with fallback and warning
        if (!PlayerPrefs.HasKey("ParticipantID"))
        {
            Debug.LogWarning("ParticipantID not found in PlayerPrefs. Using default value of 1. " +
                            "Please set ParticipantID using TrialParameterSetter.");
            participantID = 1;
            PlayerPrefs.SetInt("ParticipantID", participantID);
        }
        else
        {
            participantID = PlayerPrefs.GetInt("ParticipantID");
        }

        // Initialize phase to 1
        currentPhase = 1;
        PlayerPrefs.SetInt("Phase", currentPhase);
        PlayerPrefs.Save();

        Debug.Log($"StateManagement initialized: Participant {participantID}, Phase {currentPhase}");

        // Load participant data and apply condition settings
        StartCoroutine(InitializeCondition());

        // Start with all tabletop elements hidden
        if (menu != null) HideObject(menu);
        if (food != null) HideObject(food);
        if (survey != null) HideObject(survey);
    }

    IEnumerator InitializeCondition()
    {
        // Load Participants.csv
        string csvPath = Path.Combine(Application.streamingAssetsPath, participantsCsvName);
        string csvContent = "";

        // Handle different platforms (PC and Quest 3)
        if (csvPath.Contains("://") || csvPath.Contains("jar:"))
        {
            // Android/Quest platform
            UnityWebRequest www = UnityWebRequest.Get(csvPath);
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to load {participantsCsvName}: {www.error}");
                yield break;
            }
            else
            {
                csvContent = www.downloadHandler.text;
            }
        }
        else
        {
            // PC/Standalone platform
            if (File.Exists(csvPath))
            {
                csvContent = File.ReadAllText(csvPath);
            }
            else
            {
                Debug.LogError($"{participantsCsvName} not found at {csvPath}");
                yield break;
            }
        }

        // Parse CSV and find participant's condition
        if (ParseParticipantsCSV(csvContent))
        {
            ApplyConditionSettings();
        }
    }

    bool ParseParticipantsCSV(string csvText)
    {
        if (string.IsNullOrEmpty(csvText))
        {
            Debug.LogError("CSV content is empty");
            return false;
        }

        string[] lines = csvText.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

        // Skip header row (index 0)
        for (int i = 1; i < lines.Length; i++)
        {
            string[] cols = lines[i].Split(',');
            if (cols.Length < 2) continue;

            ParticipantData data = new ParticipantData();

            if (int.TryParse(cols[0].Trim(), out data.participantID) &&
                int.TryParse(cols[1].Trim(), out data.condition))
            {
                allParticipants.Add(data);
            }
        }

        Debug.Log($"Loaded {allParticipants.Count} participant records");
        return allParticipants.Count > 0;
    }

    void ApplyConditionSettings()
    {
        // Find this participant's condition
        ParticipantData participantData = allParticipants.Find(p => p.participantID == participantID);

        if (participantData == null)
        {
            Debug.LogWarning($"No condition found for Participant {participantID}. Using default condition 1.");
            condition = 1;
        }
        else
        {
            condition = participantData.condition;
        }

        Debug.Log($"Applying settings for Condition {condition}");

        // Apply post-processing adjustments based on condition
        if (postProcessingVolume == null)
        {
            Debug.LogWarning("No Post Processing Volume assigned!");
            return;
        }

        // Get the Color Adjustments component for Exposure
        UnityEngine.Rendering.Universal.ColorAdjustments colorAdjustments;
        if (!postProcessingVolume.profile.TryGet(out colorAdjustments))
        {
            Debug.LogError("ColorAdjustments component not found in Post Processing Volume profile!");
            return;
        }

        // Get the White Balance component for Temperature
        UnityEngine.Rendering.Universal.WhiteBalance whiteBalance;
        if (!postProcessingVolume.profile.TryGet(out whiteBalance))
        {
            Debug.LogError("WhiteBalance component not found in Post Processing Volume profile!");
            return;
        }

        // Apply settings based on condition
        switch (condition)
        {
            case 1:
                // Condition 1: Exposure -0.5, Temperature -20
                colorAdjustments.postExposure.value = -0.5f;
                whiteBalance.temperature.value = -20f;
                Debug.Log("Applied Condition 1: Exposure -0.5, Temperature -20");
                break;

            case 2:
                // Condition 2: Exposure -0.5, Temperature +20
                colorAdjustments.postExposure.value = -0.5f;
                whiteBalance.temperature.value = 20f;
                Debug.Log("Applied Condition 2: Exposure -0.5, Temperature +20");
                break;

            case 3:
                // Condition 3: Exposure +0.5, Temperature -20
                colorAdjustments.postExposure.value = 0.5f;
                whiteBalance.temperature.value = -20f;
                Debug.Log("Applied Condition 3: Exposure +0.5, Temperature -20");
                break;

            case 4:
                // Condition 4: Exposure +0.5, Temperature +20
                colorAdjustments.postExposure.value = 0.5f;
                whiteBalance.temperature.value = 20f;
                Debug.Log("Applied Condition 4: Exposure +0.5, Temperature +20");
                break;

            default:
                Debug.LogWarning($"Unknown condition {condition}. No settings applied.");
                break;
        }
    }

    // Public method to advance to next phase
    public void NextPhase()
    {
        currentPhase++;
        PlayerPrefs.SetInt("Phase", currentPhase);
        PlayerPrefs.Save();
        Debug.Log($"Advanced to Phase {currentPhase}");
    }

    // Public method to set a specific phase
    public void SetPhase(int phase)
    {
        currentPhase = phase;
        PlayerPrefs.SetInt("Phase", currentPhase);
        PlayerPrefs.Save();
        Debug.Log($"Set Phase to {currentPhase}");
    }

    // Public method to reset phase
    public void ResetPhase()
    {
        currentPhase = 1;
        PlayerPrefs.SetInt("Phase", currentPhase);
        PlayerPrefs.Save();
        Debug.Log("Phase reset to 1");
    }

    // Example audio playback method
    public void PlayAudio(int index)
    {
        if (audioClips == null || audioClips.Length == 0)
        {
            Debug.LogWarning("No audio clips assigned!");
            return;
        }

        if (index < 0 || index >= audioClips.Length)
        {
            Debug.LogWarning($"Audio clip index {index} out of range!");
            return;
        }

        // You'll need an AudioSource component to play the clip
        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.clip = audioClips[index];
        audioSource.Play();
        Debug.Log($"Playing audio clip {index}: {audioClips[index].name}");
    }

    // Play audio based on current phase (Phase 1 = audio index 0)
    public void PlayAudioForCurrentPhase()
    {
        // Ensure phase is initialized
        if (!PlayerPrefs.HasKey("Phase"))
        {
            Debug.Log("Phase not found in PlayerPrefs, initializing to Phase 1");
            PlayerPrefs.SetInt("Phase", 1);
            PlayerPrefs.Save();
            currentPhase = 1;
        }
        else
        {
            // Refresh current phase from PlayerPrefs
            currentPhase = PlayerPrefs.GetInt("Phase");
        }

        int audioIndex = currentPhase - 1; // Convert phase to zero-based index

        // Validate phase/index
        if (audioIndex < 0)
        {
            Debug.LogWarning($"Invalid phase {currentPhase}. Setting to Phase 1.");
            SetPhase(1);
            audioIndex = 0;
        }

        Debug.Log($"Playing audio for Phase {currentPhase} (index {audioIndex})");
        PlayAudio(audioIndex);
    }
    public void ShowObject(GameObject obj)
    {
        if (obj == null)
        {
            Debug.LogWarning("GameObject is null - cannot show!");
            return;
        }

        if (obj.activeSelf)
        {
            Debug.LogWarning($"{obj.name} is already visible!");
            return;
        }

        obj.SetActive(true);
        Debug.Log($"{obj.name} shown");
    }

    public void HideObject(GameObject obj)
    {
        if (obj == null)
        {
            Debug.LogWarning("GameObject is null - cannot hide!");
            return;
        }

        if (!obj.activeSelf)
        {
            Debug.LogWarning($"{obj.name} is already hidden!");
            return;
        }

        obj.SetActive(false);
        Debug.Log($"{obj.name} hidden");
    }

}