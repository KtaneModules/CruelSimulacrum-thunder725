using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public class CruelSimulacrum : MonoBehaviour {

    // Bomb & Module references
    public KMBombModule thisModule;
    public KMColorblindMode colourblindManager;
    public KMRuleSeedable ruleseedManager;
    public KMAudio audioManager;

    // Buttons
    public KMSelectable TurkeyButton, WhaleButton, MoyaiButton, FinesseButton, SubmitButton;
    const float pressedButtonHeightDifference = 0.012f;
    enum TwmfSymbol { None, Turkey, Whale, Moyai, Finesse };
    readonly Dictionary<string, Coroutine> buttonPressedCoroutines = new Dictionary<string, Coroutine>();
    public AudioClip[] buttonPressAudioClips;

    // Token Slot
    public KMSelectable TokenSlot;
    public Transform TokenTransform;
    readonly Vector3 TokenAnimationStartingPosition = new Vector3(-0.07f, 0.05f, 0.055f);
    readonly Vector3 TokenAnimationEndingPosition = new Vector3(-0.07f, 0.0f, 0.055f);
    Coroutine TokenPressedCoroutine;
    bool isTokenInserted;
    public AudioClip TokenSlotAudioClip;

    // Priority LEDs
    public MeshRenderer ALedMesh, BLedMesh, CLedMesh;


    // Light
    enum LightMovements { Clockwise, CounterClockwise, Flip };
    List<LightMovements> currentStageLightMovements = new List<LightMovements>();

    public Transform lightParentTransform;
    enum LightMovementState { None, Idle, Moving };
    LightMovementState currentLightMovementState;
    const float lightRotationSpeed = 150f;
    const float shortLightIdleTimer = 0.75f;
    const float longLightIdleTimer = 2.5f;
    readonly Vector2 RotationLightTimerRange = new Vector2(0.5f, 1.0f);
    readonly Vector2 RotationLightAngleRange = new Vector2(90f, 250f);
    readonly Vector2 FlipLightAngleRange = new Vector2(10f, 20f);
    /// <summary> How long the current Light Movement has been going for </summary>
    float currentLightMovementTimer;
    /// <summary> How long the current Light Movement should go for </summary>
    float targetLightMovementTimer;
    /// <summary> Which movement in the currentStageLightMovement is being shown </summary>
    int currentlyDisplayedLightMovementIndex;
    float startingLightMovementRotation;
    float targetLightMovementRotation;


    // Grid, Symbols and Nodes
    /// <summary> Grid on Page 2, in reading order (3,5,5). Index 5 always will be None (empty) </summary>
    TwmfSymbol[] currentSymbolGrid;
    public enum RgbColour { Red, Green, Blue, Yellow, Magenta, Cyan, White };
    /// <summary> Colour of the 3 ABC Nodes in order </summary>
    RgbColour[] NodeColours;
    readonly Dictionary<RgbColour, List<RgbColour>> ColourMixing = new Dictionary<RgbColour, List<RgbColour>>()
    {
        { RgbColour.Red, new List<RgbColour>(){ RgbColour.Red} },
        { RgbColour.Green, new List<RgbColour>(){ RgbColour.Green } },
        { RgbColour.Blue, new List<RgbColour>(){ RgbColour.Blue } },
        { RgbColour.Yellow, new List<RgbColour>(){ RgbColour.Red, RgbColour.Green } },
        { RgbColour.Magenta, new List<RgbColour>(){ RgbColour.Red, RgbColour.Blue} },
        { RgbColour.Cyan, new List<RgbColour>(){ RgbColour.Green, RgbColour.Blue } },
        { RgbColour.White, new List<RgbColour>(){ RgbColour.Red, RgbColour.Green, RgbColour.Blue } },
    };
    /// <summary> Gives out which Nodes are adjacent to every triangle using their index </summary>
    readonly string[] TriangleNodeConnections = new string[13]
    {
              "A",  "A",  "A",
        "B", "AB", "ABC", "AC", "C",
         "B", "B", "BC",  "C", "C"
    };

    /// <summary> For every possible Light Movement, given any of the three Pivots, gives the new triangle index post-movement. </summary>
    readonly Dictionary<LightMovements, Dictionary<char, int[]>> BigMovementsTable = new Dictionary<LightMovements, Dictionary<char, int[]>>
    {
        {
            LightMovements.Clockwise,
            new Dictionary<char, int[]>
            {
                { 'A', new int[13]{ 1, 2, 6, -1, 0, 4, 5, -1, -1, -1, -1, -1, -1} },
                { 'B', new int[13]{ -1, -1, -1, 4, 5, 10, -1, -1, 3, 8, 9, -1, -1} },
                { 'C', new int[13]{ -1, -1, -1, -1, -1, 6, 7, 12, -1, -1, 5, 10, 11} }
            }
        },
        { 
            LightMovements.CounterClockwise,
            new Dictionary<char, int[]>
            {
                { 'A', new int[13]{ 4, 0, 1, -1, 5, 6, 2, -1, -1, -1, -1, -1, -1} },
                { 'B', new int[13]{ -1, -1, -1, 8, 3, 4, -1, -1, 9, 10, 5, -1, -1} },
                { 'C', new int[13]{ -1, -1, -1, -1, -1, 10, 5, 6, -1, -1, 11, 12, 7} }
            }
        },
        {
            LightMovements.Flip,
            new Dictionary<char, int[]>
            {
                { 'A', new int[13]{ 6, 5, 4, -1, 2, 1, 0, -1, -1, -1, -1, -1, -1} },
                { 'B', new int[13]{ -1, -1, -1, 10, 9, 8, -1, -1, 5, 4, 3, -1, -1} },
                { 'C', new int[13]{ -1, -1, -1, -1, -1, 12, 11, 10, -1, -1, 7, 6, 5} }
            }
        }
    };


    // Puzzle and Symbol Sequence, for each Stage
    // Here you go, Souvenir!
    RgbColour[] ColourPerStage;
    List<LightMovements>[] LightMovementsPerStage;
    string[] NodePriorityOrderPerStage;

    List<TwmfSymbol>[] ExpectedAnswersPerStage;



    // Current Puzzle / Stage / Input data
    int currentStageIndex;
    readonly List<TwmfSymbol> currentPlayerInput = new List<TwmfSymbol>();
    public AudioClip NewStageAudioClip, SolveAudioClip;


    // Screen
    public Text PromptText;
    public Text PromptColourText;
    public Text ColourblindModeText;
    public Image ScreenSlopImage;

    public Sprite[] RedSlopTextures, GreenSlopTextures, BlueSlopTextures, YellowSlopTextures, MagentaSlopTextures, CyanSlopTextures, WhiteSlopTextures;
    public Sprite SolveSlopTexture;

    readonly Dictionary<RgbColour, Color> ColourToHex = new Dictionary<RgbColour, Color>()
    {
        { RgbColour.Red, Color.red },
        { RgbColour.Green, new Color(0, .823f, 0) },
        { RgbColour.Blue, Color.blue },
        { RgbColour.Yellow, new Color(.925f, .784f, 0) },
        { RgbColour.Magenta, Color.magenta },
        { RgbColour.Cyan, new Color(0, .84f, .84f) },
        { RgbColour.White, Color.black },
    };

    public Image[] ScreenPlayerSubmissionImages;
    public Sprite TurkeySprite, WhaleSprite, MoyaiSprite, FinesseSprite, EllipsisSprite;


    // Logging Data
    static int moduleIdCounter = 1;
    int moduleId;
    [SerializeField] bool moduleSolved;


    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //    Vanilla
    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=

#pragma warning disable IDE0051 // Remove unused private members
    void Awake()
    {
        moduleIdCounter = 1;

        TurkeyButton.OnInteract += delegate () { SymbolButtonGetsPressed(TwmfSymbol.Turkey, TurkeyButton); return false; };
        WhaleButton.OnInteract += delegate () { SymbolButtonGetsPressed(TwmfSymbol.Whale, WhaleButton); return false; };
        MoyaiButton.OnInteract += delegate () { SymbolButtonGetsPressed(TwmfSymbol.Moyai, MoyaiButton); return false; };
        FinesseButton.OnInteract += delegate () { SymbolButtonGetsPressed(TwmfSymbol.Finesse, FinesseButton); return false; };
        SubmitButton.OnInteract += delegate () { SubmitButtonGetsPressed(); return false; };
        TokenSlot.OnInteract += delegate () { TokenSlotGetsPressed(); return false; };

        TokenTransform.transform.localPosition = TokenAnimationEndingPosition;

        currentLightMovementState = LightMovementState.None;
    }

    // Use this for initialization
    void Start ()
    {
        moduleId = moduleIdCounter++;

        ManageRuleseed();

        GenerateAllStages();

        ShowNewStage(0);

        // Souvenir Variables Testing (they work!)
        // ModuleLog(false, "Display Colours per stage: {0}", ColourPerStage.Join());
        // ModuleLog(false, "Light Movements per stage: {0}", LightMovementsPerStage.Select(x => x.Join()).Join(" // "));
        // ModuleLog(false, "Node Priority Order per stage: {0}", NodePriorityOrderPerStage.Join());
        // ModuleLog(false, "Expected Answer per stage: {0}", ExpectedAnswersPerStage.Select(x => x.Join()).Join(" // "));
    }
	
	// Update is called once per frame
	void Update ()
    {
        UpdateLightPosition();
	}
#pragma warning restore IDE0051 // Remove unused private members

    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //    Player Inputs
    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=

    void SymbolButtonGetsPressed(TwmfSymbol pressedButton, KMSelectable buttonSelectable)
    {
        string buttonSymbolString = pressedButton.ToString();

        // Visual feedback
        if (buttonPressedCoroutines.ContainsKey(buttonSymbolString))
        {
            StopCoroutine(buttonPressedCoroutines[buttonSymbolString]);
            buttonPressedCoroutines.Remove(buttonSymbolString);
        }
        float height = pressedButton == TwmfSymbol.Turkey || pressedButton == TwmfSymbol.Finesse ? 0.0214f : 0.0262f;
        buttonPressedCoroutines.Add(buttonSymbolString, StartCoroutine(VisualButtonAnimation(buttonSelectable.transform, height)));

        buttonSelectable.AddInteractionPunch(1.0f);

        // Audio feedback
        PlayRandomButtonpressSound();


        if (moduleSolved) { return; }

        if (isTokenInserted)
        {
            currentPlayerInput.Add(pressedButton);
            ModuleLog(true, "{0} Button got pressed. Current sequence is {1}",
                buttonSymbolString, currentPlayerInput.Join());
            RefreshScreenPlayerInputs();
        }
        else
        {
            ModuleLog(true, "{0} Button got pressed. You need to insert a Token before starting a submission!! !i!i! STRIKE !i!i!",
                buttonSymbolString);
            thisModule.HandleStrike();
        }
    }

    void SubmitButtonGetsPressed()
    {
        // Visual feedback
        if (buttonPressedCoroutines.ContainsKey("Submit"))
        {
            StopCoroutine(buttonPressedCoroutines["Submit"]);
            buttonPressedCoroutines.Remove("Submit");
        }
        buttonPressedCoroutines.Add("Submit", StartCoroutine(VisualButtonAnimation(SubmitButton.transform, 0.028f)));

        SubmitButton.AddInteractionPunch(1.0f);

        // Audio feedback
        PlayRandomButtonpressSound();

        if (moduleSolved) { return; }
        
        // For some reason, just asking if the two arrays are the same isn't enough; so we're checking every element 1 by 1
        if (currentPlayerInput.Count == ExpectedAnswersPerStage[currentStageIndex].Count)
        {
            bool isValid = true;
            for (int i = 0; i < currentPlayerInput.Count; i++)
            {
                if (currentPlayerInput[i] != ExpectedAnswersPerStage[currentStageIndex][i])
                {
                    isValid = false;
                    break;
                }
            }

            if (isValid) 
            {
                ModuleLog(true, "Submit Button got pressed with the valid sequence {0}!!", currentPlayerInput.Join());
                if (currentStageIndex == 2)
                {
                    SolveModule();
                    return;
                }

                currentStageIndex++;
                ShowNewStage(currentStageIndex);
                return;
            }
        }
        
        
        ModuleLog(true, "Submit Button got pressed with invalid sequence {0}. Expected {1}.",
            currentPlayerInput.Join(), ExpectedAnswersPerStage[currentStageIndex].Join());
        ClearPlayerInput();
        isTokenInserted = false;
        thisModule.HandleStrike();
             
    }

    void TokenSlotGetsPressed()
    {
        // Visual feedback
        if (TokenPressedCoroutine != null)
        {
            StopCoroutine(TokenPressedCoroutine);
        }
        TokenPressedCoroutine = StartCoroutine(VisualTokenAnimation());

        TokenSlot.AddInteractionPunch(1.0f);

        // Audio feedback
        PlaySoundAndForget(TokenSlotAudioClip);


        if (moduleSolved) { return; }
        ModuleLog(true, "You inserted a coin into the Token Slot.");

        isTokenInserted = true;
        ClearPlayerInput();
    }

    void ClearPlayerInput()
    {
        currentPlayerInput.Clear();
        RefreshScreenPlayerInputs();
    }


    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //    Puzzle Generation
    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=

    void ManageRuleseed()
    {
        MonoRandom Rng = ruleseedManager.GetRNG();

        ModuleLog(true, "Using Ruleseed {0}", Rng.Seed);

        NodeColours = new RgbColour[3] { RgbColour.Red, RgbColour.Green, RgbColour.Blue };
        if (Rng.Seed == 1)
        {
            currentSymbolGrid = new TwmfSymbol[13] { TwmfSymbol.Turkey, TwmfSymbol.Moyai, TwmfSymbol.Whale,
                TwmfSymbol.Finesse, TwmfSymbol.Whale, TwmfSymbol.None, TwmfSymbol.Finesse, TwmfSymbol.Moyai,
                TwmfSymbol.Turkey, TwmfSymbol.Finesse, TwmfSymbol.Turkey, TwmfSymbol.Moyai, TwmfSymbol.Whale };
            return;
        }

        // The same order as ruleseed 1, without None
        List<TwmfSymbol> _newSymbolOrder = new List<TwmfSymbol>(){ TwmfSymbol.Turkey, TwmfSymbol.Moyai, TwmfSymbol.Whale,
                TwmfSymbol.Finesse, TwmfSymbol.Whale, TwmfSymbol.Finesse, TwmfSymbol.Moyai,
                TwmfSymbol.Turkey, TwmfSymbol.Finesse, TwmfSymbol.Turkey, TwmfSymbol.Moyai, TwmfSymbol.Whale };
        
        // Shuffle, then insert None
        Rng.ShuffleFisherYates(_newSymbolOrder);
        _newSymbolOrder.Insert(5, TwmfSymbol.None);
        currentSymbolGrid = _newSymbolOrder.ToArray();

        ModuleLog(false, "Grid in reading order is {0}.", currentSymbolGrid.Join());

        // The Red Green Blue
        Rng.ShuffleFisherYates(NodeColours);
        ModuleLog(false, "ABC Node in order are {0}.", NodeColours.Join());
    }


    void GenerateAllStages()
    {
        ModuleLog(true, "Triangles are numbered in reading order, starting at 0. Blank one is 5, last one is 12.");

        GenerateStageColours();

        GenerateLightMovements();

        DetermineNodePriorityOrders();

        ExpectedAnswersPerStage = new List<TwmfSymbol>[3];

        for (int i = 0; i < 3; i++)
        {
            ModuleLog(true, "=-=-= Stage {0} =-=-=", i + 1);
            ModuleLog(true, "{0} Stage has Colour read {1}, Light movements being {2}, and Node priority order {3}.",
                i == 0 ? "First" : i == 1 ? "Second" : "Third",
                ColourPerStage[i].ToString(),
                LightMovementsPerStage[i].Join(", "),
                NodePriorityOrderPerStage[i]);

            // Generate stage answers one by one because logging will be done in it and it's easier
            // to not store it and just log it fully
            GenerateStageAnswer(i);
        }
    }

    void DetermineNodePriorityOrders()
    {
        NodePriorityOrderPerStage = new string[3];

        for (int i = 0; i < 3; i ++)
        {
            NodePriorityOrderPerStage[i] = new char[3] { 'A', 'B', 'C' }.Shuffle().Join("");
        }
    }

    void UpdateNodePriorityLeds(int stageIndex)
    {
        if (stageIndex == -1)
        {
            // Turn the LEDs off for Souvenir
            ALedMesh.material.SetColor("_Color", Color.black);
            BLedMesh.material.SetColor("_Color", Color.black);
            CLedMesh.material.SetColor("_Color", Color.black);
            return;
        }

        // light up the LEDs in order
        ALedMesh.material.SetColor("_Color", NodePriorityOrderPerStage[stageIndex][0] == 'A' ? Color.white : 
            NodePriorityOrderPerStage[stageIndex][1] == 'A' ? Color.grey : Color.black);

        BLedMesh.material.SetColor("_Color", NodePriorityOrderPerStage[stageIndex][0] == 'B' ? Color.white : 
            NodePriorityOrderPerStage[stageIndex][1] == 'B' ? Color.grey : Color.black);

        CLedMesh.material.SetColor("_Color", NodePriorityOrderPerStage[stageIndex][0] == 'C' ? Color.white : 
            NodePriorityOrderPerStage[stageIndex][1] == 'C' ? Color.grey : Color.black);
    }

    void GenerateStageColours()
    {
        // Generate three unique colours
        RgbColour[] possibleColours = new RgbColour[7] { RgbColour.Red, RgbColour.Green, RgbColour.Blue,
            RgbColour.Yellow, RgbColour.Magenta, RgbColour.Cyan, RgbColour.White };

        possibleColours.Shuffle();
        ColourPerStage = possibleColours.Take(3).ToArray();
    }

    void GenerateLightMovements()
    {
        LightMovementsPerStage = new List<LightMovements>[3];

        // We want the range of possible movements to steadily rise for each stage
        // 3-5; then 4-6; then 5-7

        Vector2Int[] difficultyRanges = new Vector2Int[3]
        { 
            new Vector2Int(3,6),
            new Vector2Int(4,7),
            new Vector2Int(5,8),
        };

        for (int i = 0; i < 3; i++)
        {
            LightMovementsPerStage[i] = new int[9] { 0, 0, 0, 1, 1, 1, 2, 2, 2 }
                .Shuffle()
                .Take(UnityEngine.Random.Range(difficultyRanges[i].x, difficultyRanges[i].y))
                .Select(x => (LightMovements)x)
                .ToList();
        }
    }

    void GenerateStageAnswer(int stageId)
    {
        // Indices go
        //   0  1  2
        // 3 4  5  6  7
        // 8 9  10 11 12


        // Starting triangle depending on Ruleseed
        int currentPositionIndex = DetermineStageStartingTriangle(ColourPerStage[stageId]);

        // Determine starting Pivot
        char currentPivot = GetPivotFromPriority(stageId, TriangleNodeConnections[currentPositionIndex]).Value;
        ModuleLog(true, "Start at the triangle connected to Colour(s) {0}, which is index {1}; with Pivot {2}",
            ColourMixing[ColourPerStage[stageId]].Join("-"), currentPositionIndex, currentPivot);


        // Apply every Stage
        List<LightMovements> stageLightMovements = LightMovementsPerStage[stageId];
        int _newPositionIndex;
        char? _potentialNewPivot;
        TwmfSymbol _symbolOnCurrentTriangle;

        ExpectedAnswersPerStage[stageId] = new List<TwmfSymbol>();

        foreach (LightMovements _movement in stageLightMovements)
        {
            // Determine the new Movement
            _newPositionIndex = BigMovementsTable[_movement][currentPivot][currentPositionIndex];

            // Determine if the Pivot must be changed
            _potentialNewPivot = DetermineNewPivotFromMovement(stageId, currentPositionIndex, _newPositionIndex);
            if (_potentialNewPivot.HasValue)
            {
                currentPivot = _potentialNewPivot.Value;
            }

            // Verify the symbol on the current Triangle
            _symbolOnCurrentTriangle = currentSymbolGrid[_newPositionIndex];

            // Log 
            ModuleLog(true, "Movement {0} brought the current Triangle to index {1}.{2}{3}",
                    _movement.ToString(), _newPositionIndex,
                    _potentialNewPivot.HasValue ? $"New Pivot is {_potentialNewPivot.Value}." : "",
                    _symbolOnCurrentTriangle != TwmfSymbol.None ? $" Triangle has symbol {_symbolOnCurrentTriangle}" : " Triangle has no symbol.");

            // Apply the movement data
            currentPositionIndex = _newPositionIndex;
            if (_potentialNewPivot.HasValue)
            {
                currentPivot = _potentialNewPivot.Value;
            }
            if (_symbolOnCurrentTriangle != TwmfSymbol.None)
            {
                ExpectedAnswersPerStage[stageId].Add(_symbolOnCurrentTriangle);
            }
        }

        // Log the final result.
        ModuleLog(true, "Expected Symbol Sequence for stage {0} is {1}.", stageId + 1, ExpectedAnswersPerStage[stageId].Join());
    }

    int DetermineStageStartingTriangle(RgbColour stageColour)
    {
        // ABC pegs can have different RGB starting colours due to ruleseed, so this must be determined dynamically
        int _return;

        // We can only spawn in 1-8-12 due to single-colour, 4-6-10 due to double-colour, and 5 due to triple-colour.

        // Determine the starting position for this stage
        List<RgbColour> _startingTriangleColours = ColourMixing[stageColour];
        // White => 5
        if (_startingTriangleColours.Count == 3)
        {
            _return = 5;
        }
        // Yellow Magenta Cyan => 4 6 10
        else if (_startingTriangleColours.Count == 2)
        {
            // If the Triangle doesn't contain a A, it's BC and goes to 10 directly
            // Otherwise, the presence of B will indicate if it's AB-4 or AC-6
            _return = _startingTriangleColours.Contains(NodeColours[0]) ? (_startingTriangleColours.Contains(NodeColours[1]) ? 4 : 6) : 10;
        }
        // Red Green Blue => 1 8 12
        else
        {
            // We can directly transpose A to 1, B to 8, and C to 12
            _return = _startingTriangleColours[0] == NodeColours[0] ? 1 : _startingTriangleColours[0] == NodeColours[1] ? 8 : 12;
        }

        return _return;
    }

    /// <summary> Using the previous and new triangle for a movement, as well as the current Pivot,
    /// determines if the Pivot should change and what it should become. </summary>
    char? DetermineNewPivotFromMovement(int stageId, int previousTriangleIndex, int newTriangleIndex)
    {
        string previousTriangleNodes = TriangleNodeConnections[previousTriangleIndex];
        string newTriangleNodes = TriangleNodeConnections[newTriangleIndex];

        // Remove from the New Nodes the ones that were already adjacent in the Previous Triangle:
        // They are now "new" if they were connected already!
        newTriangleNodes = newTriangleNodes.Where(c => previousTriangleNodes.Contains(c) == false).Join("");

        // Then, get the highest-priority one
        return GetPivotFromPriority(stageId, newTriangleNodes);
    }

    char? GetPivotFromPriority(int stageId, string adjacentNodes)
    {
        string stageNodePriorityOrder = NodePriorityOrderPerStage[stageId];

        // For all 3 letters in order in the current Priority Order for this stage
        // If that letter is present in the Adjacent Nodes, then return it and quit.
        for (int i = 0; i < 3; i ++)
        {
            if (adjacentNodes.Contains(NodePriorityOrderPerStage[stageId][i]))
            { return NodePriorityOrderPerStage[stageId][i]; }
        }

        return null;
    }




    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //    Visual Methods
    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=

    void ShowNewStage(int newStageIndex)
    {
        currentStageIndex = newStageIndex;
        isTokenInserted = false;

        // Priority LEDs
        UpdateNodePriorityLeds(newStageIndex);

        // Light Movement
        currentStageLightMovements = LightMovementsPerStage[currentStageIndex];
        currentLightMovementState = LightMovementState.None;
        currentlyDisplayedLightMovementIndex = 999;
        LightFinishesIdle();


        ClearPlayerInput();

        // Screen
        RgbColour colourToShow = ColourPerStage[currentStageIndex];

        // Different for the very first Stage and the next ones
        if (newStageIndex == 0)
        {
            // Prompt
            PromptText.gameObject.SetActive(true);
            PromptColourText.gameObject.SetActive(true);
            PromptColourText.text = colourToShow.ToString();
            PromptColourText.color = ColourToHex[colourToShow];

            // Slop Image
            ScreenSlopImage.gameObject.SetActive(false);

            // Colourblind Mode
            ColourblindModeText.gameObject.SetActive(false);
        }
        else
        {
            // Prompt
            PromptText.gameObject.SetActive(false);
            PromptColourText.gameObject.SetActive(false);

            // Slop Image
            ScreenSlopImage.gameObject.SetActive(true);
            ScreenSlopImage.sprite = GetRandomSlopImageFromColour(colourToShow);
            StartCoroutine(MakeSlopAppear());

            // Colourblind Mode
            ColourblindModeText.gameObject.SetActive(colourblindManager.ColorblindModeActive);
            ColourblindModeText.text = colourToShow.ToString();
            ColourblindModeText.color = ColourToHex[colourToShow];

            // Audio
            PlaySoundAndForget(NewStageAudioClip);
        }
    }

    IEnumerator VisualButtonAnimation(Transform buttonToMove, float endingHeight)
    {
        const float maxTime = 0.6f;

        float timer = 0;
        float position;
        while (timer < maxTime)
        {
            timer += Time.deltaTime;

            position = Mathf.Lerp(endingHeight-pressedButtonHeightDifference, endingHeight, Easing.OutQuint(Mathf.InverseLerp(0, maxTime, timer), 0, 1, 1));

            buttonToMove.transform.localPosition = new Vector3(buttonToMove.localPosition.x, position, buttonToMove.localPosition.z);

            // Wait for next frame
            yield return null;
        }

        buttonToMove.transform.localPosition = new Vector3(buttonToMove.localPosition.x, endingHeight, buttonToMove.localPosition.z);
    }

    IEnumerator VisualTokenAnimation()
    {
        const float maxTime = 0.3f;

        float timer = 0;
        Vector3 position;
        while (timer < maxTime)
        {
            timer += Time.deltaTime;

            position = Vector3.Lerp(TokenAnimationStartingPosition, TokenAnimationEndingPosition, Easing.OutCubic(Mathf.InverseLerp(0, maxTime, timer), 0, 1, 1));

            TokenTransform.transform.localPosition = position;

            // Wait for next frame
            yield return null;
        }

        TokenTransform.transform.localPosition = TokenAnimationEndingPosition;
    }

    /// <summary> Called every frame on Update. Serves as the root for this State Machine </summary>
    void UpdateLightPosition()
    {
        // Rotate Clockwise indefinitely
        if (moduleSolved)
        {
            lightParentTransform.localEulerAngles += Vector3.up * Time.deltaTime * lightRotationSpeed;
            return;
        }

        // The Stages haven't been initialized yet
        if (currentStageLightMovements.Count == 0 || currentLightMovementState == LightMovementState.None)
        {
            currentLightMovementState = LightMovementState.None;
            return;
        }

        currentLightMovementTimer += Time.deltaTime;

        // Currently Idle
        if (currentLightMovementState == LightMovementState.Idle)
        {
            // Timer reached the end, start moving again
            if (currentLightMovementTimer >= targetLightMovementTimer)
            {
                LightFinishesIdle();
                return;
            }

            // Otherwise... well wait, don't do anything
            return;
        }
        // Currently moving
        else if (currentLightMovementState == LightMovementState.Moving)
        {
            // Timer reached the end, go to Idle
            if (currentLightMovementTimer >= targetLightMovementTimer)
            {
                LightFinishedMovement();
                return;
            }

            // Timer still progressing, keep moving
            ApplyLightRotation();
            return;
        }
    }

    void ApplyLightRotation()
    {
        // Flip has a particular way of rotating
        if (currentStageLightMovements[currentlyDisplayedLightMovementIndex] == LightMovements.Flip)
        {
            // It is a Sine wave (0 -> 1 -> 0 -> -1 -> 0) that offset the current rotation then returns to normal
            // So its input should lerp from 0 to 2pi 
            float flipLerpValue = Easing.InOutQuad(Mathf.InverseLerp(0, targetLightMovementTimer, currentLightMovementTimer), 0, 1, 1);
            float flipSinValue = 2 * Mathf.PI * flipLerpValue;

            // Then it creates an offset
            float flipAngleOffset = Mathf.Sin(flipSinValue) * targetLightMovementRotation;
            lightParentTransform.localEulerAngles = Vector3.up * (startingLightMovementRotation + flipAngleOffset);
            return;
        }

        // The other Clockwise & Widdershins are regular movements
        float lerpTValue = Mathf.InverseLerp(0, targetLightMovementTimer, currentLightMovementTimer);
        lerpTValue = Easing.InOutCubic(lerpTValue, 0, 1, 1);
        lightParentTransform.localEulerAngles = Vector3.Lerp(Vector3.up * startingLightMovementRotation, Vector3.up * targetLightMovementRotation, lerpTValue);
    }

    void LightFinishedMovement()
    {
        currentLightMovementState = LightMovementState.Idle;
        currentLightMovementTimer = 0;

        // Has the entire sequence been finished
        if (currentlyDisplayedLightMovementIndex >= currentStageLightMovements.Count - 1)
        {
            // Do a small pause before hiding the light
            currentlyDisplayedLightMovementIndex = 999;
        }
        else
        {
            targetLightMovementTimer = shortLightIdleTimer;
            currentlyDisplayedLightMovementIndex++;
        }
        return;
    }

    void LightFinishesIdle()
    {
        // Light has been hidden at the end of the sequence, if needed re-enable it
        if (currentlyDisplayedLightMovementIndex == -1)
        {
            currentLightMovementState = LightMovementState.Idle;

            lightParentTransform.gameObject.SetActive(true);
            lightParentTransform.localEulerAngles = Vector3.up * UnityEngine.Random.Range(0f, 360f);
            startingLightMovementRotation = lightParentTransform.localEulerAngles.y;

            // And after re-appearing, do another small pause
            currentlyDisplayedLightMovementIndex++;
            currentLightMovementTimer = 0;
            targetLightMovementTimer = shortLightIdleTimer;
            return;
        }

        // Light has stayed there at the end of the sequence, enters another Idle that hides the light
        if (currentlyDisplayedLightMovementIndex >= currentStageLightMovements.Count)
        {
            currentLightMovementState = LightMovementState.Idle;

            lightParentTransform.gameObject.SetActive(false);
            targetLightMovementTimer = longLightIdleTimer;
            currentlyDisplayedLightMovementIndex = -1;
            return;
        }

        // Otherwise, calculate stuff!
        currentLightMovementState = LightMovementState.Moving;
        currentLightMovementTimer = 0;

        startingLightMovementRotation = lightParentTransform.localEulerAngles.y;
        switch (currentStageLightMovements[currentlyDisplayedLightMovementIndex])
        {
            case LightMovements.Clockwise:
                targetLightMovementRotation = startingLightMovementRotation + UnityEngine.Random.Range(RotationLightAngleRange.x, RotationLightAngleRange.y);
                targetLightMovementTimer = UnityEngine.Random.Range(RotationLightTimerRange.x, RotationLightTimerRange.y);
                break;

            case LightMovements.CounterClockwise:
                targetLightMovementRotation = startingLightMovementRotation - UnityEngine.Random.Range(RotationLightAngleRange.x, RotationLightAngleRange.y);
                targetLightMovementTimer = UnityEngine.Random.Range(RotationLightTimerRange.x, RotationLightTimerRange.y);
                break;

            case LightMovements.Flip:
                // Flip is particular, its target is a singular value within range, relative to start
                // Since it uses a cosine to move
                targetLightMovementRotation = UnityEngine.Random.Range(FlipLightAngleRange.x, FlipLightAngleRange.y);
                targetLightMovementTimer = UnityEngine.Random.Range(RotationLightTimerRange.x, RotationLightTimerRange.y);
                break;
        }
    }

    Sprite GetRandomSlopImageFromColour(RgbColour colour)
    {
        switch (colour)
        {
            case RgbColour.Red: return RedSlopTextures.PickRandom();
            case RgbColour.Green: return GreenSlopTextures.PickRandom();
            case RgbColour.Blue: return BlueSlopTextures.PickRandom();
            case RgbColour.Yellow: return YellowSlopTextures.PickRandom();
            case RgbColour.Magenta: return MagentaSlopTextures.PickRandom();
            case RgbColour.Cyan: return CyanSlopTextures.PickRandom();
            case RgbColour.White: return WhiteSlopTextures.PickRandom();
        }
        return null;
    }

    Sprite GetSymbolFromEnum(TwmfSymbol symbol)
    {
        switch (symbol)
        {
            case TwmfSymbol.None: return EllipsisSprite;
            case TwmfSymbol.Turkey: return TurkeySprite;
            case TwmfSymbol.Whale: return WhaleSprite;
            case TwmfSymbol.Moyai: return MoyaiSprite;
            case TwmfSymbol.Finesse: return FinesseSprite;
        }

        return null;
    }

    void RefreshScreenPlayerInputs()
    {
        const float distance = 225f;

        // Ignore everything in sequences that are too long to show
        int numberOfSubmissions = Mathf.Min(currentPlayerInput.Count, ScreenPlayerSubmissionImages.Length);
        // float angleBetweenImages = Mathf.Lerp(25, 10, Mathf.InverseLerp(2, 6, numberOfSubmissions));
        float angleBetweenImages = 12.5f;

        float OffsetFromCenter;
        Vector3 _position = Vector3.zero;
        float imageAngle;

        for (int i = 0; i < ScreenPlayerSubmissionImages.Length; i++)
        {
            if (i >= numberOfSubmissions)
            {
                ScreenPlayerSubmissionImages[i].gameObject.SetActive(false);
                continue;
            }

            // Show the image
            ScreenPlayerSubmissionImages[i].gameObject.SetActive(true);

            // Determine the angle of the picture from the horizontal 180°
            OffsetFromCenter = i - ((float)(numberOfSubmissions - 1) / 2);
            imageAngle = (180 + (OffsetFromCenter * angleBetweenImages)) * Mathf.Deg2Rad;

            // Use that to get coordinates
            _position.x = Mathf.Cos(imageAngle) * distance;
            _position.y = Mathf.Sin(imageAngle) * distance;
            ScreenPlayerSubmissionImages[i].rectTransform.anchoredPosition = _position;

            // Set the sprite, with the last one always being an Ellipsis
            ScreenPlayerSubmissionImages[i].sprite = GetSymbolFromEnum(i == (ScreenPlayerSubmissionImages.Length - 1) ? TwmfSymbol.None : currentPlayerInput[i]);
        }
    }

    IEnumerator MakeSlopAppear()
    {
        const float maxTime = 0.7f;

        float timer = 0;
        float scale;
        while (timer < maxTime)
        {
            timer += Time.deltaTime;

            scale = Mathf.Lerp(1.5f, 1, Easing.BackOut(Mathf.InverseLerp(0, maxTime, timer), 0, 1, 1, 5));

            ScreenSlopImage.transform.localScale = Vector3.one * scale;

            // Wait for next frame
            yield return null;
        }

        ScreenSlopImage.transform.localScale = Vector3.one;
    }

    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //    Other Methods
    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=

    void SolveModule()
    {
        moduleSolved = true;
        thisModule.HandlePass();

        // Clear the Screen
        ClearPlayerInput();
        ColourblindModeText.gameObject.SetActive(false);

        // Spawn the final Slop image
        ScreenSlopImage.sprite = SolveSlopTexture;
        StartCoroutine(MakeSlopAppear());

        // Turn the LEDs off for Souvenir
        UpdateNodePriorityLeds(-1);

        PlaySoundAndForget(SolveAudioClip);

        ModuleLog(true, "Module Solved");
    }

    /// <summary> Simplified method to just play a sound from an AudioClip and forget about it </summary>
    void PlaySoundAndForget(AudioClip soundToPlay)
    {
        audioManager.PlaySoundAtTransform(soundToPlay.name, transform);
    }

    void PlayRandomButtonpressSound()
    {
        PlaySoundAndForget(buttonPressAudioClips.PickRandom());
    }

    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //    Logging
    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=

    public void ModuleLog(bool LogInLfa, string message, params object[] args)
    {
        if (LogInLfa)
        { Debug.LogFormat("[Cruel Simulacrum #{0}] {1}", moduleId, string.Format(message, args)); }
        else
        { Debug.LogFormat("<Cruel Simulacrum #{0}> {1}", moduleId, string.Format(message, args)); }
    }

    public void ModuleLogWarning(bool LogInLfa, string message, params object[] args)
    {
        if (LogInLfa)
        { Debug.LogWarningFormat("[Cruel Simulacrum #{0}] {1}", moduleId, string.Format(message, args)); }
        else
        { Debug.LogWarningFormat("<Cruel Simulacrum #{0}> {1}", moduleId, string.Format(message, args)); }
    }

    public void ModuleLogError(bool LogInLfa, string message, params object[] args)
    {
        if (LogInLfa)
        { Debug.LogErrorFormat("[Cruel Simulacrum #{0}] {1}", moduleId, string.Format(message, args)); }
        else
        { Debug.LogErrorFormat("<Cruel Simulacrum #{0}> {1}", moduleId, string.Format(message, args)); }
    }


    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //    Twitch Plays
    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=

#pragma warning disable IDE0051 // Remove unused private members
    private readonly string TwitchHelpMessage = @"“!{0} KTMFWS” to press Token then Turkey Moyai Finesse Whale then Submit in this order. Full button names also work.";

    IEnumerator ProcessTwitchCommand(string command)
    {
        // Credit to Royal_Flu$h for this line 
        var commandParts = command.ToLowerInvariant().Split(new[] { ' ', ',', ';', '-' }, StringSplitOptions.RemoveEmptyEntries);

        // There is also the possibility of using just the KTMFWS initials instead of full names; so let's handle that
        List<string> allCommands = new List<string>();
        // This will also serve as a verification
        foreach (var _word in commandParts)
        {
            // Match on all words containing exclusively letters from KTMFWS
            if (Regex.IsMatch(_word, @"\b[ktmfws]+\b", RegexOptions.IgnoreCase))
            {
                foreach (char c in _word)
                {
                    allCommands.Add(c.ToString());
                }
                continue;
            }

            // Verify instead that it's one of our allowed strings, with misspellings too that's okay
            switch (_word)
            {
                case "token": allCommands.Add("k"); continue;
                case "turkey": allCommands.Add("t"); continue;
                case "moyai": case "moai": allCommands.Add("m"); continue;
                case "finesse": case "finese": case "pinch": allCommands.Add("f"); continue;
                case "whale": allCommands.Add("w"); continue;
                case "submit": allCommands.Add("s"); continue;
            }

            // If we arrive here, we found a word that's neither one of our wanted words nor one of its initials
            yield return "sendtochaterror {0} Unknown received command: '" + _word + "'. Submission cancelled.";
            yield break;
        }

        // If we arrive here on the other hand, it means that everything was valid!
        yield return null;
        foreach (var _command in allCommands)
        {
            switch (_command)
            {
                case "k": TokenSlot.OnInteract(); break;
                case "t": TurkeyButton.OnInteract(); break;
                case "m": MoyaiButton.OnInteract(); break;
                case "f": FinesseButton.OnInteract(); break;
                case "w": WhaleButton.OnInteract(); break;
                case "s": SubmitButton.OnInteract(); break;
            }
            yield return new WaitForSeconds(0.2f);
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        ModuleLog(true, "Received Twitch Plays Forced Solve");

        Dictionary<TwmfSymbol, KMSelectable> ButtonFromSymbol = new Dictionary<TwmfSymbol, KMSelectable>()
        {
            { TwmfSymbol.Turkey, TurkeyButton },
            { TwmfSymbol.Whale, WhaleButton },
            { TwmfSymbol.Moyai, MoyaiButton },
            { TwmfSymbol.Finesse, FinesseButton },
        };

        yield return null;

        // For all three stages (or however many we have left at this point)
        for (int i = currentStageIndex; i < 3; i++)
        {
            // Do the first Token Slot / Reset Sequence
            TokenSlot.OnInteract();
            yield return new WaitForSeconds(0.2f);

            // Press all expected symbols
            foreach (TwmfSymbol expectedPress in ExpectedAnswersPerStage[i])
            {
                ButtonFromSymbol[expectedPress].OnInteract();
                yield return new WaitForSeconds(0.2f);
            }

            // Submit
            SubmitButton.OnInteract();
            yield return new WaitForSeconds(0.5f);
        }
    }
#pragma warning restore IDE0051 // Remove unused private members
}
