using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public enum MathOperator { Addition, Subtraction, Multiplication, Division, Simple, Advanced }
public enum Difficulty { Beginner, Intermediate, Difficult, Adaptive }

public class MathGameManager : MonoBehaviour
{
    [Header("Stats Profile UI")]
    public GameObject statsPanel;
    public TextMeshProUGUI statsText;
    public Button showStatsButton;
    public Button closeStatsButton;

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
    public TextMeshProUGUI totalPointsText;
    public TextMeshProUGUI streakText;
    public TextMeshProUGUI feedbackText;

    [Header("Game Enhancement UI (Optional)")]
    public Slider comboBar;
    public TextMeshProUGUI nextMilestoneText;
    public GameObject sessionSummaryPanel;
    public TextMeshProUGUI sessionSummaryText;
    public Button sessionSummaryOkButton;
    public GameObject achievementNotificationPanel;
    public TextMeshProUGUI achievementNotificationText;

    [Header("Leaderboard UI")]
    public GameObject leaderboardPanel;
    public TextMeshProUGUI leaderboardText;
    public TMP_InputField nameEntryInput;
    public Button submitNameButton;
    public Button continueButton;
    public Button exitGameButton;

    [Header("Streak Animation")]
    public GameObject streakMessageObj;
    public TextMeshProUGUI streakMessageText;
    public string[] encouragementMessages = { "On Fire!", "Unstoppable!", "Math Genius!", "Keep It Up!", "Amazing!" };

    #region Serializable Classes

    [System.Serializable]
    private class Achievement
    {
        public string id;
        public string name;
        public string description;
        public bool unlocked;
        public System.DateTime unlockedDate;
    }

    [System.Serializable]
    private class SessionStats
    {
        public int correctAnswers;
        public int incorrectAnswers;
        public int maxStreak;
        public float maxComboScore;
        public float sessionDurationSeconds;
        public int operatorUsed;
        public int difficultyUsed;
        public List<string> achievementsUnlockedThisSession = new List<string>();
    }

    [System.Serializable]
    private class PlayerProfile
    {
        public List<Achievement> achievements = new List<Achievement>();
        public SessionStats lastSession;
        public int totalGamesPlayed;
        public int totalQuestionsAnswered;
    }

    [System.Serializable]
    private class LeaderboardEntry
    {
        public string name;
        public int streak;
        public float comboScore;
        public float timeToMilestoneSeconds;
        public long timestamp;
    }

    [System.Serializable]
    private class LeaderboardData
    {
        public LeaderboardEntry[] entries;
    }

    #endregion

    #region Game State

    private MathOperator currentOp;
    private Difficulty currentDiff;
    private Difficulty selectedDiff;
    private Difficulty currentAdaptiveDiff;
    private bool isGameActive = false;
    private bool isAdaptiveMode = false;

    private int currentAnswer;
    private int correctAnswers = 0;
    private int incorrectAnswers = 0;
    private int currentStreak = 0;
    private int maxStreakThisSession = 0;
    private float maxStreakTime = 0f;
    private bool awaitingNameEntry = false;

    // Time tracking
    private float gameStartTime = 0f;
    private float streakStartTime = 0f;

    // Combo multiplier
    private float currentMultiplier = 1.0f;
    private float sessionComboScore = 0f;
    private const float BasePointsPerQuestion = 10f;

    // Leaderboard caching for rank
    private List<LeaderboardEntry> currentStreakLeaderboard;
    private List<LeaderboardEntry> currentComboLeaderboard;

    // Adaptive difficulty
    private int consecutiveCorrect = 0;
    private int consecutiveIncorrect = 0;
    private const int AdaptiveThresholdUp = 10;
    private const int AdaptiveThresholdDown = 5;

    // Player persistence
    private PlayerProfile playerProfile;
    private const string PlayerProfilePrefsKey = "PlayerProfile";
    private List<Achievement> allAchievements;

    #endregion

    private void Start()
    {
        InitializeAchievements();
        LoadPlayerProfile();
        ShowMenu();

        // Button listeners
        if (showStatsButton != null) showStatsButton.onClick.AddListener(ShowStats);
        if (closeStatsButton != null) closeStatsButton.onClick.AddListener(CloseStats);
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (checkAnswerButton != null) checkAnswerButton.onClick.AddListener(CheckAnswer);
        if (nextQuestionButton != null) nextQuestionButton.onClick.AddListener(NextQuestion);
        if (backToMenuButton != null) backToMenuButton.onClick.AddListener(ShowMenu);
        if (endGameButton != null) endGameButton.onClick.AddListener(EndGame);
        if (submitNameButton != null) submitNameButton.onClick.AddListener(SubmitNameFromButton);
        if (continueButton != null) continueButton.onClick.AddListener(ContinueFromLeaderboard);
        if (exitGameButton != null) exitGameButton.onClick.AddListener(QuitGame);
        if (sessionSummaryOkButton != null) sessionSummaryOkButton.onClick.AddListener(ProceedToLeaderboard);

        if (operatorDropdown != null) operatorDropdown.onValueChanged.AddListener(OnOperatorChanged);
        if (difficultyDropdown != null) difficultyDropdown.onValueChanged.AddListener(OnDifficultyChanged);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);

        if (answerInputField != null)
        {
            answerInputField.keyboardType = TouchScreenKeyboardType.NumberPad;
            answerInputField.onSubmit.AddListener(delegate { CheckAnswer(); });
        }
    }

    private void Update()
    {
        if (isGameActive && gamePanel != null && gamePanel.activeSelf && answerInputField != null && !answerInputField.isFocused && answerInputField.interactable)
        {
            answerInputField.Select();
            answerInputField.ActivateInputField();
        }
    }

    #region Initialization & Persistence

    private void InitializeAchievements()
    {
        allAchievements = new List<Achievement>
        {
            new Achievement { id = "first_3_streak", name = "Beginner", description = "Reach a 3-question streak" },
            new Achievement { id = "10_streak", name = "Performer", description = "Reach a 10-question streak" },
            new Achievement { id = "20_streak", name = "Master", description = "Reach a 20-question streak" },
            new Achievement { id = "50_streak", name = "Legend", description = "Reach a 50-question streak" },
            new Achievement { id = "lightning_3", name = "Lightning", description = "3-streak in under 15 seconds" },
            new Achievement { id = "speedrun_3", name = "Speedrun", description = "3-streak in under 10 seconds" },
            new Achievement { id = "variety", name = "Variety", description = "Reach 5-streak in all 4 operators" },
            new Achievement { id = "difficult_master", name = "Difficulty Climber", description = "Reach 10-streak on Difficult" },
            new Achievement { id = "perfect_session", name = "Perfect Session", description = "10 consecutive correct answers" },
            new Achievement { id = "combo_master", name = "Combo Master", description = "Reach 4x multiplier" }
        };
    }

    private void LoadPlayerProfile()
    {
        string json = PlayerPrefs.GetString(PlayerProfilePrefsKey, "");
        if (string.IsNullOrEmpty(json))
        {
            playerProfile = new PlayerProfile();
            playerProfile.achievements = new List<Achievement>(allAchievements);
            SavePlayerProfile();
        }
        else
        {
            try
            {
                playerProfile = JsonUtility.FromJson<PlayerProfile>(json);
                if (playerProfile.achievements == null) playerProfile.achievements = new List<Achievement>();
                
                foreach (var ach in allAchievements)
                {
                    if (!playerProfile.achievements.Any(a => a.id == ach.id))
                        playerProfile.achievements.Add(ach);
                }
            }
            catch
            {
                playerProfile = new PlayerProfile();
                playerProfile.achievements = new List<Achievement>(allAchievements);
            }
        }
    }

    private void SavePlayerProfile()
    {
        string json = JsonUtility.ToJson(playerProfile);
        PlayerPrefs.SetString(PlayerProfilePrefsKey, json);
        PlayerPrefs.Save();
    }

    #endregion

    #region Stats Profile

    public void ShowStats()
    {
        if (statsPanel == null || statsText == null) return;
        statsPanel.SetActive(true);
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>Stats Profile</b>\n");
        sb.AppendLine($"Total Games Played: {playerProfile.totalGamesPlayed}");
        sb.AppendLine($"Total Questions Answered: {playerProfile.totalQuestionsAnswered}\n");
        sb.AppendLine("<b>Unlocked Achievements:</b>");
        
        int unlockedCount = 0;
        foreach (var ach in playerProfile.achievements)
        {
            if (ach.unlocked)
            {
                sb.AppendLine($"- <b>{ach.name}</b>: {ach.description}");
                unlockedCount++;
            }
        }
        if (unlockedCount == 0) sb.AppendLine("<i>None yet. Keep playing!</i>");

        statsText.text = sb.ToString();
    }

    public void CloseStats()
    {
        if (statsPanel != null) statsPanel.SetActive(false);
    }

    #endregion

    #region Menu & Game Flow

    public void ShowMenu()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
        if (gamePanel != null) gamePanel.SetActive(false);
        if (sessionSummaryPanel != null) sessionSummaryPanel.SetActive(false);
        if (statsPanel != null) statsPanel.SetActive(false);
        if (startButton != null) startButton.gameObject.SetActive(true);
        if (streakMessageObj != null) streakMessageObj.SetActive(false);
        if (comboBar != null) comboBar.gameObject.SetActive(false);
        
        HideLeaderboardPanel();
        isGameActive = false;
        awaitingNameEntry = false;
    }

    public void StartGame()
    {
        RefreshSettingsFromDropdowns();
        HideLeaderboardPanel();
        if (sessionSummaryPanel != null) sessionSummaryPanel.SetActive(false);
        
        awaitingNameEntry = false;
        correctAnswers = 0;
        incorrectAnswers = 0;
        currentStreak = 0;
        maxStreakThisSession = 0;
        maxStreakTime = 0f;
        currentMultiplier = 1.0f;
        sessionComboScore = 0f;
        consecutiveCorrect = 0;
        consecutiveIncorrect = 0;

        gameStartTime = Time.time;
        streakStartTime = Time.time;

        currentStreakLeaderboard = LoadLeaderboard(GetLeaderboardKey("Streak"));
        currentComboLeaderboard = LoadLeaderboard(GetLeaderboardKey("Combo"));

        isAdaptiveMode = (selectedDiff == Difficulty.Adaptive);
        if (isAdaptiveMode)
            currentAdaptiveDiff = Difficulty.Beginner;
        else
            currentDiff = selectedDiff;

        if (playerProfile.lastSession == null)
            playerProfile.lastSession = new SessionStats();
        playerProfile.lastSession.achievementsUnlockedThisSession.Clear();

        UpdateScoreUI();

        if (menuPanel != null) menuPanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(true);
        if (startButton != null) startButton.gameObject.SetActive(false);
        if (comboBar != null) comboBar.gameObject.SetActive(true);
        
        isGameActive = true;
        GenerateQuestion();
    }

    private void OnOperatorChanged(int newValue)
    {
        RefreshSettingsFromDropdowns();
        if (isGameActive) GenerateQuestion();
    }

    private void OnDifficultyChanged(int newValue)
    {
        RefreshSettingsFromDropdowns();
        if (isGameActive && !isAdaptiveMode) GenerateQuestion();
    }

    private T GetSelectedEnumValue<T>(TMP_Dropdown dropdown) where T : struct, System.Enum
    {
        if (dropdown != null && dropdown.options != null && dropdown.options.Count > dropdown.value)
        {
            var label = dropdown.options[dropdown.value].text;
            if (!string.IsNullOrEmpty(label) && System.Enum.TryParse(label, out T result))
                return result;
        }
        return default(T);
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
        selectedDiff = GetSelectedEnumValue<Difficulty>(difficultyDropdown);
    }

    #endregion

    #region Question Generation

    private void NextQuestion()
    {
        RefreshSettingsFromDropdowns();
        GenerateQuestion();
    }

    private void GenerateQuestion()
    {
        RefreshSettingsFromDropdowns();
        Difficulty diffToUse = isAdaptiveMode ? currentAdaptiveDiff : selectedDiff;

        if (feedbackText != null) feedbackText.text = "";
        if (answerInputField != null)
        {
            answerInputField.text = "";
            answerInputField.interactable = true;
        }
        if (checkAnswerButton != null) checkAnswerButton.gameObject.SetActive(true);
        if (nextQuestionButton != null) nextQuestionButton.gameObject.SetActive(false);

        int num1 = 0, num2 = 0;
        int min1 = 1, max1 = 9;
        int min2 = 1, max2 = 9;

        switch (diffToUse)
        {
            case Difficulty.Beginner:
                min1 = 1; max1 = 9;
                min2 = 1; max2 = 9;
                break;
            case Difficulty.Intermediate:
                min1 = 1; max1 = 9;
                min2 = 10; max2 = 99;
                break;
            case Difficulty.Difficult:
                min1 = 10; max1 = 99;
                min2 = 10; max2 = 99;
                break;
        }

        num1 = Random.Range(min1, max1 + 1);
        num2 = Random.Range(min2, max2 + 1);

        if (diffToUse == Difficulty.Intermediate && Random.value < 0.5f)
        {
            int temp = num1;
            num1 = num2;
            num2 = temp;
        }

        string opSymbol = "+";
        MathOperator opToUse = currentOp;

        if (opToUse == MathOperator.Simple)
        {
            opToUse = Random.value < 0.5f ? MathOperator.Addition : MathOperator.Subtraction;
        }
        else if (opToUse == MathOperator.Advanced)
        {
            opToUse = (MathOperator)Random.Range(0, 4); // 0=Add, 1=Sub, 2=Mul, 3=Div
        }

        switch (opToUse)
        {
            case MathOperator.Addition:
                opSymbol = "+";
                currentAnswer = num1 + num2;
                break;
            case MathOperator.Subtraction:
                opSymbol = "-";
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
                currentAnswer = num1;
                num1 = currentAnswer * num2;
                break;
        }

        if (questionText != null) questionText.text = $"{num1} {opSymbol} {num2} =";
        StartCoroutine(FocusInputField());
    }

    private IEnumerator FocusInputField()
    {
        yield return null;
        if (answerInputField != null)
        {
            answerInputField.Select();
            answerInputField.ActivateInputField();
        }
    }

    #endregion

    #region Gameplay Logic (Answers, Achievements, Adaptive)

    public void CheckAnswer()
    {
        if (answerInputField == null || string.IsNullOrEmpty(answerInputField.text)) return;

        if (int.TryParse(answerInputField.text, out int userAnswer))
        {
            answerInputField.interactable = false;
            if (checkAnswerButton != null) checkAnswerButton.gameObject.SetActive(false);
            if (nextQuestionButton != null) nextQuestionButton.gameObject.SetActive(true);

            if (userAnswer == currentAnswer)
            {
                HandleCorrectAnswer();
            }
            else
            {
                HandleIncorrectAnswer();
            }

            playerProfile.totalQuestionsAnswered++;
            SavePlayerProfile();

            UpdateScoreUI();
            EvaluateAchievements();

            if (nextQuestionButton != null) nextQuestionButton.Select();
        }
    }

    private void HandleCorrectAnswer()
    {
        correctAnswers++;
        currentStreak++;
        
        float currentStreakTime = Time.time - streakStartTime;
        if (currentStreak > maxStreakThisSession)
        {
            maxStreakThisSession = currentStreak;
            maxStreakTime = currentStreakTime;
        }
        else if (currentStreak == maxStreakThisSession)
        {
            if (currentStreakTime < maxStreakTime || maxStreakTime == 0f)
                maxStreakTime = currentStreakTime;
        }

        consecutiveCorrect++;
        consecutiveIncorrect = 0;

        float pointsEarned = BasePointsPerQuestion * currentMultiplier;
        sessionComboScore += pointsEarned;
        
        // Increase multiplier (e.g. +0.5x per correct)
        currentMultiplier += 0.5f;

        if (feedbackText != null) feedbackText.text = "<color=green>Correct!</color>";
        PlaySound("Correct");

        if (currentStreak >= 3)
        {
            ShowStreakMessage();
        }

        // Adaptive Difficulty Shift Up
        if (isAdaptiveMode && consecutiveCorrect >= AdaptiveThresholdUp)
        {
            if (currentAdaptiveDiff == Difficulty.Beginner) 
            {
                currentAdaptiveDiff = Difficulty.Intermediate;
                consecutiveCorrect = 0;
                ShowFeedbackMessage("Flow Mode: Difficulty Increased to Intermediate!", Color.yellow);
                PlaySound("Milestone");
            }
            else if (currentAdaptiveDiff == Difficulty.Intermediate)
            {
                currentAdaptiveDiff = Difficulty.Difficult;
                consecutiveCorrect = 0;
                ShowFeedbackMessage("Flow Mode: Difficulty Increased to Difficult!", Color.yellow);
                PlaySound("Milestone");
            }
        }
    }

    private void HandleIncorrectAnswer()
    {
        incorrectAnswers++;
        currentStreak = 0;
        consecutiveIncorrect++;
        consecutiveCorrect = 0;
        currentMultiplier = 1.0f; // Reset multiplier
        streakStartTime = Time.time; // Reset streak timer

        if (feedbackText != null) feedbackText.text = $"<color=red>Incorrect.</color> Answer is {currentAnswer}.";
        PlaySound("Incorrect");

        // Adaptive Difficulty Shift Down
        if (isAdaptiveMode && consecutiveIncorrect >= AdaptiveThresholdDown)
        {
            if (currentAdaptiveDiff == Difficulty.Difficult) 
            {
                currentAdaptiveDiff = Difficulty.Intermediate;
                consecutiveIncorrect = 0;
                ShowFeedbackMessage("Flow Mode: Difficulty Decreased to Intermediate.", Color.cyan);
            }
            else if (currentAdaptiveDiff == Difficulty.Intermediate)
            {
                currentAdaptiveDiff = Difficulty.Beginner;
                consecutiveIncorrect = 0;
                ShowFeedbackMessage("Flow Mode: Difficulty Decreased to Beginner.", Color.cyan);
            }
        }
    }

    private void EvaluateAchievements()
    {
        float streakTime = Time.time - streakStartTime;
        
        UnlockAchievementIf("first_3_streak", currentStreak >= 3);
        UnlockAchievementIf("10_streak", currentStreak >= 10);
        UnlockAchievementIf("20_streak", currentStreak >= 20);
        UnlockAchievementIf("50_streak", currentStreak >= 50);
        
        UnlockAchievementIf("lightning_3", currentStreak >= 3 && streakTime < 15f);
        UnlockAchievementIf("speedrun_3", currentStreak >= 3 && streakTime < 10f);

        if (currentStreak >= 10 && currentDiff == Difficulty.Difficult)
            UnlockAchievementIf("difficult_master", true);

        if (consecutiveCorrect >= 10)
            UnlockAchievementIf("perfect_session", true);

        if (currentMultiplier >= 4.0f)
            UnlockAchievementIf("combo_master", true);
    }

    private void UnlockAchievementIf(string id, bool condition)
    {
        if (!condition) return;

        var ach = playerProfile.achievements.FirstOrDefault(a => a.id == id);
        if (ach != null && !ach.unlocked)
        {
            ach.unlocked = true;
            ach.unlockedDate = System.DateTime.Now;
            playerProfile.lastSession.achievementsUnlockedThisSession.Add(ach.name);
            SavePlayerProfile();
            ShowAchievementNotification(ach);
        }
    }

    private void ShowAchievementNotification(Achievement ach)
    {
        PlaySound("Achievement");
        if (achievementNotificationPanel != null && achievementNotificationText != null)
        {
            achievementNotificationText.text = $"Achievement Unlocked:\n<b>{ach.name}</b>\n<size=80%>{ach.description}</size>";
            StartCoroutine(ShowNotificationCoroutine(achievementNotificationPanel, 3f));
        }
    }

    private IEnumerator ShowNotificationCoroutine(GameObject panel, float duration)
    {
        panel.SetActive(true);
        yield return new WaitForSeconds(duration);
        panel.SetActive(false);
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null) 
        {
            scoreText.text = $"<color=#4CAF50>{correctAnswers}</color> - <color=#F44336>{incorrectAnswers}</color>";
        }
        
        if (totalPointsText != null)
        {
            totalPointsText.text = $"⭐ {Mathf.FloorToInt(sessionComboScore)}";
        }
        if (streakText != null) streakText.text = $"🔥 {currentStreak}";
        
        if (nextMilestoneText != null)
        {
            int nextMilestone = ((currentStreak / 5) + 1) * 5;
            if (currentStreak < 3) nextMilestone = 3;
            int remaining = nextMilestone - currentStreak;
            nextMilestoneText.text = $"{remaining} to next milestone!";
            
            if (comboBar != null)
            {
                int prevMilestone = nextMilestone == 3 ? 0 : nextMilestone - 5;
                comboBar.minValue = prevMilestone;
                comboBar.maxValue = nextMilestone;
                comboBar.value = currentStreak;
            }
        }
    }

    private int GetRank(List<LeaderboardEntry> lb, float currentScore, bool isStreak)
    {
        if (lb == null) return 11;
        int rank = 1;
        foreach (var entry in lb)
        {
            float entryScore = isStreak ? entry.streak : entry.comboScore;
            if (currentScore >= entryScore) break;
            rank++;
        }
        return rank;
    }

    private void ShowStreakMessage()
    {
        if (streakMessageObj == null || streakMessageText == null) return;
        string msg = encouragementMessages[Random.Range(0, encouragementMessages.Length)];
        streakMessageText.text = msg;
        StartCoroutine(AnimateMessage(streakMessageObj));
    }

    private void ShowFeedbackMessage(string msg, Color color)
    {
        if (streakMessageObj == null || streakMessageText == null) return;
        streakMessageText.text = $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{msg}</color>";
        StartCoroutine(AnimateMessage(streakMessageObj));
    }

    private IEnumerator AnimateMessage(GameObject obj)
    {
        obj.SetActive(true);
        float timer = 0;
        while (timer < 2f)
        {
            float scale = Mathf.PingPong(Time.time * 3, 0.3f) + 1f;
            obj.transform.localScale = new Vector3(scale, scale, 1);
            timer += Time.deltaTime;
            yield return null;
        }
        obj.SetActive(false);
    }

    private void PlaySound(string soundType)
    {
        // Placeholder for future audio implementation
        Debug.Log($"[Audio Hook] PlaySound: {soundType}");
    }

    #endregion

    #region Session Summary & Leaderboard

    private string GetLeaderboardKey(string metric)
    {
        string diffStr = isAdaptiveMode ? "Adaptive" : selectedDiff.ToString();
        string opStr = currentOp.ToString();
        return $"LB_{metric}_{diffStr}_{opStr}";
    }

    private List<LeaderboardEntry> LoadLeaderboard(string key)
    {
        var list = new List<LeaderboardEntry>();
        string json = PlayerPrefs.GetString(key, "");
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

    private void SaveLeaderboard(string key, List<LeaderboardEntry> list)
    {
        var data = new LeaderboardData { entries = list.ToArray() };
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
    }

    private bool QualifiesForLeaderboard(int streak, float comboScore)
    {
        string streakKey = GetLeaderboardKey("Streak");
        string comboKey = GetLeaderboardKey("Combo");

        var streakList = LoadLeaderboard(streakKey);
        var comboList = LoadLeaderboard(comboKey);

        bool qualifiesStreak = streakList.Count < 10 || streak > streakList.Min(e => e.streak);
        bool qualifiesCombo = comboList.Count < 10 || comboScore > comboList.Min(e => e.comboScore);

        return qualifiesStreak || qualifiesCombo;
    }

    private void AddToLeaderboards(string name)
    {
        string finalName = string.IsNullOrEmpty(name) ? "Anonymous" : name;
        
        string streakKey = GetLeaderboardKey("Streak");
        var streakList = LoadLeaderboard(streakKey);
        streakList.Add(new LeaderboardEntry { name = finalName, streak = maxStreakThisSession, comboScore = sessionComboScore, timeToMilestoneSeconds = maxStreakTime, timestamp = System.DateTime.Now.Ticks });
        streakList.Sort((a, b) => {
            int streakCompare = b.streak.CompareTo(a.streak);
            if (streakCompare == 0) return a.timeToMilestoneSeconds.CompareTo(b.timeToMilestoneSeconds);
            return streakCompare;
        });
        if (streakList.Count > 10) streakList.RemoveRange(10, streakList.Count - 10);
        SaveLeaderboard(streakKey, streakList);

        string comboKey = GetLeaderboardKey("Combo");
        var comboList = LoadLeaderboard(comboKey);
        comboList.Add(new LeaderboardEntry { name = finalName, streak = maxStreakThisSession, comboScore = sessionComboScore, timeToMilestoneSeconds = maxStreakTime, timestamp = System.DateTime.Now.Ticks });
        comboList.Sort((a, b) => b.comboScore.CompareTo(a.comboScore));
        if (comboList.Count > 10) comboList.RemoveRange(10, comboList.Count - 10);
        SaveLeaderboard(comboKey, comboList);
    }

    public void EndGame()
    {
        isGameActive = false;
        float sessionDuration = Time.time - gameStartTime;

        if (gamePanel != null) gamePanel.SetActive(false);
        if (comboBar != null) comboBar.gameObject.SetActive(false);
        if (answerInputField != null) answerInputField.interactable = false;

        playerProfile.lastSession = new SessionStats
        {
            correctAnswers = correctAnswers,
            incorrectAnswers = incorrectAnswers,
            maxStreak = maxStreakThisSession,
            maxComboScore = sessionComboScore,
            sessionDurationSeconds = sessionDuration,
            operatorUsed = (int)currentOp,
            difficultyUsed = (int)(isAdaptiveMode ? Difficulty.Adaptive : selectedDiff),
            achievementsUnlockedThisSession = playerProfile.lastSession.achievementsUnlockedThisSession
        };
        playerProfile.totalGamesPlayed++;
        SavePlayerProfile();

        if (QualifiesForLeaderboard(maxStreakThisSession, sessionComboScore) && maxStreakThisSession > 0)
        {
            awaitingNameEntry = true;
        }

        ShowSessionSummary();
    }

    private void ShowSessionSummary()
    {
        if (sessionSummaryPanel != null && sessionSummaryText != null)
        {
            sessionSummaryPanel.SetActive(true);
            
            string achText = playerProfile.lastSession.achievementsUnlockedThisSession.Count > 0 
                ? $"\nAchievements Unlocked: {string.Join(", ", playerProfile.lastSession.achievementsUnlockedThisSession)}" 
                : "";
                
            sessionSummaryText.text = $"<b>Session Summary</b>\n\n" +
                                      $"Correct: {correctAnswers}\n" +
                                      $"Max Streak: {maxStreakThisSession}\n" +
                                      $"Combo Score: {sessionComboScore}\n" +
                                      achText;
        }
        else
        {
            ProceedToLeaderboard();
        }
    }

    public void ProceedToLeaderboard()
    {
        if (sessionSummaryPanel != null) sessionSummaryPanel.SetActive(false);
        ShowLeaderboardPanel(awaitingNameEntry);
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

        if (submitNameButton != null) submitNameButton.gameObject.SetActive(showNameEntry);
        if (continueButton != null) continueButton.gameObject.SetActive(!showNameEntry);
        if (exitGameButton != null) exitGameButton.gameObject.SetActive(!showNameEntry);
    }

    private void HideLeaderboardPanel()
    {
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
    }

    private void UpdateLeaderboardUI()
    {
        if (leaderboardText == null) return;
        
        string streakKey = GetLeaderboardKey("Streak");
        var streakList = LoadLeaderboard(streakKey);
        
        string comboKey = GetLeaderboardKey("Combo");
        var comboList = LoadLeaderboard(comboKey);

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        sb.AppendLine($"<b>--- Top Streaks ({(isAdaptiveMode ? "Adaptive" : selectedDiff.ToString())} {currentOp}) ---</b>");
        for (int i = 0; i < streakList.Count; i++)
        {
            string timeStr = streakList[i].timeToMilestoneSeconds > 0 ? $" <size=80%>({streakList[i].timeToMilestoneSeconds:F1}s)</size>" : "";
            sb.AppendLine($"{i + 1}. {streakList[i].name} - Streak: {streakList[i].streak}{timeStr}");
        }
        
        sb.AppendLine($"\n<b>--- Top Combo Scores ({(isAdaptiveMode ? "Adaptive" : selectedDiff.ToString())} {currentOp}) ---</b>");
        for (int i = 0; i < comboList.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {comboList[i].name} - Pts: {comboList[i].comboScore}");
        }

        leaderboardText.text = sb.ToString();
    }

    public void SubmitNameFromButton()
    {
        if (!awaitingNameEntry) return;
        string name = nameEntryInput != null ? nameEntryInput.text : "";
        AddToLeaderboards(name);
        awaitingNameEntry = false;
        
        UpdateLeaderboardUI();
        ShowLeaderboardPanel(false);
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
}