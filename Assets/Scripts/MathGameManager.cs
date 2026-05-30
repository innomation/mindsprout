using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

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

    [Header("Score UI")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI streakText;
    public TextMeshProUGUI feedbackText;

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

    private void Start()
    {
        ShowMenu();
        startButton.onClick.AddListener(StartGame);
        checkAnswerButton.onClick.AddListener(CheckAnswer);
        nextQuestionButton.onClick.AddListener(NextQuestion);
        backToMenuButton.onClick.AddListener(ShowMenu);

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
        isGameActive = false;
    }

    public void StartGame()
    {
        RefreshSettingsFromDropdowns();

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

        questionText.text = $"{num1} {opSymbol} {num2} = ";

        StartCoroutine(FocusInputField());
    }

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