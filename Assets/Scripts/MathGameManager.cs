using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public enum MathOperator { Addition, Subtraction, Multiplication, Division }
public enum Difficulty { Beginner, Intermediate, Difficult }

public class MathGameManager : MonoBehaviour
{
    [Header("Menu UI")]
    public GameObject menuPanel;
    public TMP_Dropdown operatorDropdown;
    public TMP_Dropdown difficultyDropdown;
    public Button startButton;
    public Button quitButton;

    [Header("Game UI")]
    public GameObject gamePanel;
    public TextMeshProUGUI questionText;
    public TMP_InputField answerInputField;
    public Button checkAnswerButton;
    public Button nextQuestionButton;
    public Button backToMenuButton;
    public Button endGameButton;

    [Header("Score UI")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI streakText;
    public TextMeshProUGUI feedbackText;

    [Header("Leaderboard UI")]
    public GameObject leaderboardPanel; // panel that shows leaderboard and name entry
    public TextMeshProUGUI leaderboardText; // area that lists top 10
    public TMP_InputField nameEntryInput; // input for entering player's name when they qualify
    public Button submitNameButton;
    public Button continueButton;
    public Button exitGameButton;

    [Header("Streak Animation")]
    public GameObject streakMessageObj;
    public TextMeshProUGUI streakMessageText;
    public string[] encouragementMessages = { "On Fire!", "Unstoppable!", "Math Genius!", "Keep It Up!", "Amazing!" };

    private MathOperator currentOp;
    private Difficulty currentDiff;
    private bool isGameActive = false;

    private int currentAnswer;
    private int correctAnswers = 0;
    private int incorrectAnswers = 0;
    private int currentStreak = 0;
    private int pendingStreak = 0;
    private bool awaitingNameEntry = false;

    private const string LeaderboardPrefsKey = "LeaderboardData";

    private void Start()
    {
        ShowMenu();
        startButton.onClick.AddListener(StartGame);
        checkAnswerButton.onClick.AddListener(CheckAnswer);
        nextQuestionButton.onClick.AddListener(NextQuestion);
        backToMenuButton.onClick.AddListener(ShowMenu);
        if (endGameButton != null)
            endGameButton.onClick.AddListener(EndGame);
        if (submitNameButton != null)
            submitNameButton.onClick.AddListener(SubmitNameFromButton);
        if (continueButton != null)
            continueButton.onClick.AddListener(ContinueFromLeaderboard);
        if (exitGameButton != null)
            exitGameButton.onClick.AddListener(QuitGame);

        // Runtime listeners are wired here for manual UI wiring.
        // If buttons are assigned, Submit/Continue/Exit will work.

        operatorDropdown.onValueChanged.AddListener(OnOperatorChanged);
        difficultyDropdown.onValueChanged.AddListener(OnDifficultyChanged);
        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        // Ensure iOS numeric keyboard
        answerInputField.keyboardType = TouchScreenKeyboardType.NumberPad;

        // Submit answer on enter (or "Done" on iOS keyboard)
        answerInputField.onSubmit.AddListener(delegate { CheckAnswer(); });
    }

    private void Update()
    {
        // If the scene is missing an EventSystem, this loop can spam the Canvas and cause it to turn invisible!
        if (gamePanel.activeSelf && !answerInputField.isFocused && answerInputField.interactable)
        {
            answerInputField.Select();
            answerInputField.ActivateInputField();
        }
    }

    public void ShowMenu()
    {
        menuPanel.SetActive(true);
        gamePanel.SetActive(false);
        startButton.gameObject.SetActive(true);
        streakMessageObj.SetActive(false);
        HideLeaderboardPanel();
        isGameActive = false;
        awaitingNameEntry = false;
        pendingStreak = 0;
    }

    public void StartGame()
    {
        RefreshSettingsFromDropdowns();
        HideLeaderboardPanel();
        awaitingNameEntry = false;
        pendingStreak = 0;

        correctAnswers = 0;
        incorrectAnswers = 0;
        currentStreak = 0;
        UpdateScoreUI();

        menuPanel.SetActive(false);
        gamePanel.SetActive(true);
        startButton.gameObject.SetActive(false);
        isGameActive = true;

        GenerateQuestion();
    }

    private void OnOperatorChanged(int newValue)
    {
        RefreshSettingsFromDropdowns();
        if (isGameActive)
            GenerateQuestion();
    }

    private void OnDifficultyChanged(int newValue)
    {
        RefreshSettingsFromDropdowns();
        if (isGameActive)
            GenerateQuestion();
    }

    private T GetSelectedEnumValue<T>(TMP_Dropdown dropdown) where T : struct, System.Enum
    {
        if (dropdown.options != null && dropdown.options.Count > dropdown.value)
        {
            var label = dropdown.options[dropdown.value].text;
            if (!string.IsNullOrEmpty(label) && System.Enum.TryParse(label, out T result))
                return result;
        }

        return (T)System.Enum.ToObject(typeof(T), dropdown.value);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void RefreshSettingsFromDropdowns()
    {
        currentOp = GetSelectedEnumValue<MathOperator>(operatorDropdown);
        currentDiff = GetSelectedEnumValue<Difficulty>(difficultyDropdown);
    }

    private void NextQuestion()
    {
        RefreshSettingsFromDropdowns();
        GenerateQuestion();
    }

    private void GenerateQuestion()
    {
        RefreshSettingsFromDropdowns();

        feedbackText.text = "";
        answerInputField.text = "";
        answerInputField.interactable = true;
        checkAnswerButton.gameObject.SetActive(true);
        nextQuestionButton.gameObject.SetActive(false);

        int num1 = 0;
        int num2 = 0;

        int min1 = 1, max1 = 9;
        int min2 = 1, max2 = 9;

        switch (currentDiff)
        {
            case Difficulty.Beginner:
                min1 = 1; max1 = 9;
                min2 = 1; max2 = 9;
                break;
            case Difficulty.Intermediate: // Mix of single and double digits
                min1 = 1; max1 = 9;
                min2 = 10; max2 = 99;
                break;
            case Difficulty.Difficult: // Double digits
                min1 = 10; max1 = 99;
                min2 = 10; max2 = 99;
                break;
        }

        num1 = Random.Range(min1, max1 + 1);
        num2 = Random.Range(min2, max2 + 1);

        // For Intermediate difficulty, randomly swap operands so either
        // the first or second number can be the single-digit value.
        if (currentDiff == Difficulty.Intermediate && Random.value < 0.5f)
        {
            int temp = num1;
            num1 = num2;
            num2 = temp;
        }

        string opSymbol = "+";

        switch (currentOp)
        {
            case MathOperator.Addition:
                opSymbol = "+";
                currentAnswer = num1 + num2;
                break;
            case MathOperator.Subtraction:
                opSymbol = "-";
                // Ensure positive result to avoid negative numbers for beginners
                if (num1 < num2)
                {
                    int temp = num1;
                    num1 = num2;
                    num2 = temp;
                }
                currentAnswer = num1 - num2;
                break;
            case MathOperator.Multiplication:
                opSymbol = "x";
                currentAnswer = num1 * num2;
                break;
            case MathOperator.Division:
                opSymbol = "÷";
                // Ensure clean division by multiplying first
                currentAnswer = num1; // This is the resulting answer
                num1 = currentAnswer * num2; // The dividend
                break;
        }

        questionText.text = $"{num1} {opSymbol} {num2} =";

        StartCoroutine(FocusInputField());
    }

    #region Leaderboard

    [System.Serializable]
    private class LeaderboardEntry
    {
        public string name;
        public int streak;
    }

    [System.Serializable]
    private class LeaderboardData
    {
        public LeaderboardEntry[] entries;
    }

    private List<LeaderboardEntry> LoadLeaderboard()
    {
        var list = new List<LeaderboardEntry>();
        string json = PlayerPrefs.GetString(LeaderboardPrefsKey, "");
        if (string.IsNullOrEmpty(json)) return list;

        try
        {
            LeaderboardData data = JsonUtility.FromJson<LeaderboardData>(json);
            if (data != null && data.entries != null)
                list.AddRange(data.entries);
        }
        catch { }

        return list;
    }

    private void SaveLeaderboard(List<LeaderboardEntry> list)
    {
        var data = new LeaderboardData();
        data.entries = list.ToArray();
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(LeaderboardPrefsKey, json);
        PlayerPrefs.Save();
    }

    private bool QualifiesForLeaderboard(int streak)
    {
        var list = LoadLeaderboard();
        if (list.Count < 10) return true;
        int min = int.MaxValue;
        foreach (var e in list) if (e.streak < min) min = e.streak;
        return streak > min;
    }

    private void AddToLeaderboard(string name, int streak)
    {
        var list = LoadLeaderboard();
        var entry = new LeaderboardEntry { name = string.IsNullOrEmpty(name) ? "Anonymous" : name, streak = streak };
        list.Add(entry);
        list.Sort((a, b) => b.streak.CompareTo(a.streak));
        if (list.Count > 10) list.RemoveRange(10, list.Count - 10);
        SaveLeaderboard(list);
        UpdateLeaderboardUI(list);
    }

    private void UpdateLeaderboardUI(List<LeaderboardEntry> list = null)
    {
        if (leaderboardText == null) return;
        if (list == null) list = LoadLeaderboard();
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            sb.AppendLine($"{i + 1}. {e.name} - {e.streak}");
        }
        leaderboardText.text = sb.ToString();
    }

    private void ShowLeaderboardPanel(bool showNameEntry)
    {
        if (leaderboardPanel == null) return;
        UpdateLeaderboardUI();
        leaderboardPanel.SetActive(true);

        if (nameEntryInput != null)
        {
            nameEntryInput.gameObject.SetActive(showNameEntry);
            if (showNameEntry)
            {
                nameEntryInput.text = "";
                nameEntryInput.Select();
                nameEntryInput.ActivateInputField();
            }
        }

        if (submitNameButton != null)
            submitNameButton.gameObject.SetActive(showNameEntry);

        if (continueButton != null)
            continueButton.gameObject.SetActive(!showNameEntry);

        if (exitGameButton != null)
            exitGameButton.gameObject.SetActive(!showNameEntry);
    }

    private void HideLeaderboardPanel()
    {
        if (leaderboardPanel == null) return;
        leaderboardPanel.SetActive(false);
    }

    private void OnSubmitName()
    {
        if (!awaitingNameEntry) return;
        string name = nameEntryInput != null ? nameEntryInput.text : "";
        AddToLeaderboard(name, pendingStreak);
        awaitingNameEntry = false;
        pendingStreak = 0;
        ShowLeaderboardPanel(false);
    }

    // Public wrapper methods so buttons can be wired as persistent listeners
    public void ShowLeaderboard()
    {
        ShowLeaderboardPanel(false);
    }

    public void ShowLeaderboardWithNameEntry()
    {
        ShowLeaderboardPanel(true);
    }

    public void EndGame()
    {
        isGameActive = false;
        if (gamePanel != null)
            gamePanel.SetActive(false);
        if (answerInputField != null)
            answerInputField.interactable = false;

        if (awaitingNameEntry)
        {
            ShowLeaderboardPanel(true);
        }
        else
        {
            ShowLeaderboardPanel(false);
        }
    }

    public void SubmitNameFromButton()
    {
        OnSubmitName();
    }

    public void ContinueFromLeaderboard()
    {
        HideLeaderboardPanel();
        ShowMenu();
    }

    public void ExitGameFromLeaderboard()
    {
        QuitGame();
    }

    #endregion

    private IEnumerator FocusInputField()
    {
        yield return null;
        answerInputField.Select();
        answerInputField.ActivateInputField();
    }

    public void CheckAnswer()
    {
        if (string.IsNullOrEmpty(answerInputField.text)) return;

        int userAnswer;
        if (int.TryParse(answerInputField.text, out userAnswer))
        {
            answerInputField.interactable = false;
            checkAnswerButton.gameObject.SetActive(false);
            nextQuestionButton.gameObject.SetActive(true);

            if (userAnswer == currentAnswer)
            {
                correctAnswers++;
                currentStreak++;
                feedbackText.text = "<color=green>Correct!</color>";

                // If this streak qualifies for the leaderboard, track it and flag for name entry.
                if (QualifiesForLeaderboard(currentStreak))
                {
                    pendingStreak = currentStreak;
                    if (!awaitingNameEntry)
                        awaitingNameEntry = true;
                }

                if (currentStreak >= 3)
                {
                    ShowStreakMessage();
                }
            }
            else
            {
                incorrectAnswers++;
                currentStreak = 0;
                feedbackText.text = $"<color=red>Incorrect.</color> Answer is {currentAnswer}.";
            }

            UpdateScoreUI();

            // On iOS, focus the Next button so the keyboard can briefly dismiss, 
            // or the user can tap next question easily
            nextQuestionButton.Select();
        }
    }

    private void UpdateScoreUI()
    {
        scoreText.text = $"Score: <color=green>{correctAnswers}</color> - <color=red>{incorrectAnswers}</color>";
        streakText.text = $"Streak: {currentStreak}";
    }

    private void ShowStreakMessage()
    {
        string msg = encouragementMessages[Random.Range(0, encouragementMessages.Length)];
        streakMessageText.text = msg;
        StartCoroutine(AnimateStreakMessage());
    }

    private IEnumerator AnimateStreakMessage()
    {
        streakMessageObj.SetActive(true);
        float timer = 0;
        // Simple pulsing animation
        while (timer < 2f)
        {
            float scale = Mathf.PingPong(Time.time * 3, 0.3f) + 1f;
            streakMessageObj.transform.localScale = new Vector3(scale, scale, 1);
            timer += Time.deltaTime;
            yield return null;
        }
        streakMessageObj.SetActive(false);
    }
}