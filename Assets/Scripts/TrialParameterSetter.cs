using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TrialParameterSetter : MonoBehaviour
{
    [Header("UI Component References")]
    public TMP_InputField ParticipantIDText;
    public TMP_Text WarningText;
    public string ExperimentSceneName;

    // This method gets called by your button's OnClick event
    public void OnSubmitButtonClicked()
    {
        // Clear previous warnings
        WarningText.text = "";

        bool isValid = true;

        // Get participant ID when button is clicked
        if (int.TryParse(ParticipantIDText.text, out int participantID))
        {
            Debug.Log($"Participant ID: {participantID}");
        }
        else
        {
            WarningText.text += $"Invalid Participant: '{ParticipantIDText.text}'. ";
            isValid = false;
        }

        if (isValid)
        {
            WarningText.text = "Set";

            // Use Participant ID as PlayerPrefs
            PlayerPrefs.SetInt("ParticipantID", participantID);

            // Check if ExperimentSceneName is set
            if (string.IsNullOrEmpty(ExperimentSceneName))
            {
                WarningText.text += " Experiment scene name is not set.";
                return;
            }
            else
            {
                // Load the experiment scene
                UnityEngine.SceneManagement.SceneManager.LoadScene(ExperimentSceneName);
            }
        }
    }
}