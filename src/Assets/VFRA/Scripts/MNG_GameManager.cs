﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using UnityEngine.SceneManagement;

public class MNG_GameManager : Photon.MonoBehaviour
{

    static MNG_GameManager instance;
    public static Team[] getTeams { get { return instance.teamList; } }
    public Team[] teamList;
    public PlayerGBInfo[] playersGBInfoList;

    //ZONE VAL'
    public GameObject[] ThiefZonesPosition;
    public int max_Zones = 4;
    public List<GameObject> ZonesList;
    int ZoneActuel_Index =0;

    // ENDGAME
    public GameObject EndGamePanel;
    public bool GameFinished;
    public int WinnerTeam;

    // START
    public GameObject StartGamePanel;
    public bool WaitingToStart;

    // GAME_ROUND
    public int durationRound = 1000*60*3;
    public int minPlayers = 4;
    public int durationRoundPreparation = 10000;
    public int durationRoundFinalize = 5000;

    //DISPLAY
    public Text text_Gamestatus;
    public Text text_RoundNumber;
    public Text text_Timer;
    public Text text_btn_Ready;
    public GameObject go_checkmark_Ready;
    MNG_MainMenu mng_main;

    //PRISON
    public GameObject go_Prison;
    public static Vector3 getPrisonLocation {get{ return instance.go_Prison.transform.position; }}

    //GAMEBOARD
    public GameObject content_Gameboard;
    public GameObject gop_Gameboard_playerinfo;

    public static PlayerInitializer PlayerAvatar;
    public GameObject MainMenuCameraRoot;

    private void Awake()
    {
        instance = this;
        mng_main = FindObjectOfType<MNG_MainMenu>();
        ZonesList = new List<GameObject>();
    }

    public void chooseTeam(int value)
    {
        if (PhotonNetwork.inRoom 
            && 
                (PhotonNetwork.room.GetRoomState()==GameState.WarmUp) 
                 || 
                ( !(PhotonNetwork.player.GetPlayerState()== PlayerState.inGame)
                && !PhotonNetwork.player.GetAttribute(PlayerAttributes.HASSPAWNED,false) 
                && !PhotonNetwork.player.GetAttribute(PlayerAttributes.ISREADY, false)))
        {
            PhotonNetwork.player.SetAttribute(PlayerAttributes.TEAM, value);
            foreach (Team team in teamList) {
                if (team.Btn_join != null) team.Btn_join.interactable = true;
                if (team.BtnCheckmark != null) team.BtnCheckmark.SetActive(false);
            }
            teamList[value].BtnCheckmark.SetActive(true);
            teamList[value].Btn_join.interactable = false;
        }
    }
    bool isMinimumPlayersReady()
    { // IF NEED MASTERCLIENT LIKE KF Game = PhotonNetwork.playerList.AsEnumerable().Any(c => c.IsMasterClient && c.GetAttribute(PlayerAttributes.ISREADY, false))
        return PhotonNetwork.playerList.AsEnumerable().Count(c => ((c.getTeamID() == 1 || c.getTeamID() == 2) && c.GetAttribute(PlayerAttributes.ISREADY, false))) >= minPlayers;
    }


    void Update ()
    {
        if (!PhotonNetwork.inRoom) return;

        text_Gamestatus.text = (PhotonNetwork.room.GetRoomState()).ToString();

        if ( PhotonNetwork.room.GetAttribute(RoomAttributes.PLAYERSCANSPAWN,false)
            && PhotonNetwork.player.getTeamID()!=0
            && !PhotonNetwork.player.GetAttribute(PlayerAttributes.HASSPAWNED, false))
        {
            SpawnMyAvatar();
        }

        // ON CHECK LA PRESENCE D'UN MASTER PLAYER // UNIQUEMENT SI CE N'EST PAS EN COURS // REVIENS A : PhotonNetwork.isNonMasterClientInRoom
        if (!PhotonNetwork.playerList.AsEnumerable().Any(a=>a.isGameOwner()) && !PhotonNetwork.room.GetAttribute(RoomAttributes.CHANGEINGMASTER, false))
        {
            // ON LOCK LA METHODE POUR EVITER QUE TOUT LES JOUEURS CHANGENT L'HOST !
            PhotonNetwork.room.SetAttribute(RoomAttributes.CHANGEINGMASTER, true);
            //On le designe automatiquement par le premier joueur de la liste
            PhotonNetwork.SetMasterClient(PhotonNetwork.playerList[0]);
            // ON DELOCK
            PhotonNetwork.room.SetAttribute(RoomAttributes.CHANGEINGMASTER, false);
        }

        // CONTROLLER DE STATUT DE JEU = GEREE PAR LE PLAYERMASTER
        if ( PhotonNetwork.player.isGameOwner())
        {
            if (PhotonNetwork.room.GetRoomState() == GameState.isWaitingNewGame)
            {
                ChatVik.SendRoomMessage("Warmup : Waiting " + minPlayers + " players minimum to start game");
                StartCoroutine(WarmUp());
            }
            else if(PhotonNetwork.room.GetRoomState() == GameState.BeginningRound)
            {
                StartCoroutine(PrepareRound());
            }
            else if (PhotonNetwork.room.GetRoomState() == GameState.RoundRunning )
            {
                ManageWaypoints();
                UpdateGameInfos();
            }

            
        }

        if (Input.GetKeyDown(KeyCode.F10)){
            photonView.RPC("rpc_UnspawnPlayerAvatar", PhotonTargets.All, new object[] { PhotonNetwork.player});
        }
        UpdateTimer();
    }
    public void UpdateTimer()
    {
        if (PhotonNetwork.room.GetRoomState() == GameState.RoundRunning)
            text_Timer.text = ( (durationRound - PhotonNetwork.ServerTimestamp - PhotonNetwork.room.GetAttribute(RoomAttributes.TIMEROUNDSTARTED, PhotonNetwork.ServerTimestamp)) / 1000).ToString() + "s";
        else
            text_Timer.text = "-";
    }
    public void ManageWaypoints()
    {
        if (ZonesList.Count < max_Zones)
        {
            if (ZoneActuel_Index >= ThiefZonesPosition.Length) ZoneActuel_Index = 0;
            print("NEW ZONE");
            ZonesList.Add(PhotonNetwork.Instantiate("Thief_zone"
                , instance.ThiefZonesPosition[ZoneActuel_Index].transform.position
               , Quaternion.identity, 0));
            ZoneActuel_Index++;
        }
    }

    public void OnCreatedRoom()
    {
        if(PhotonNetwork.inRoom && !PhotonNetwork.isNonMasterClientInRoom)
        {
            PhotonNetwork.room.IsVisible = true;
            PhotonNetwork.room.IsOpen = true;
            PhotonNetwork.room.SetRoomState( GameState.isWaitingNewGame);
            InitRoomAttributes(true);
            InvokeRepeating("RefreshGameBoard", 0f, 1f);
        }
    }

    public void switchReadyState()
    {
        if (!PhotonNetwork.inRoom
            && !PhotonNetwork.player.GetAttribute(PlayerAttributes.HASSPAWNED, false)
            && PhotonNetwork.player.GetPlayerState()!=PlayerState.inGame
            ) return;
        bool newVal = !PhotonNetwork.player.GetAttribute<bool>(PlayerAttributes.ISREADY, false);
        PhotonNetwork.player.SetAttribute(PlayerAttributes.ISREADY, newVal);
        text_btn_Ready.text = newVal ? "Ready" : "Not Ready";
        ChatVik.SendRoomMessage(PhotonNetwork.player.NickName + " is now " + (newVal ? "ready" : "not ready"));
        go_checkmark_Ready.SetActive(newVal);

    }
    void OnJoinedRoom()
    {
        ChatVik.SendRoomMessage(PhotonNetwork.player.NickName + " enter the game");
        InitPlayerAttributes(PhotonNetwork.player,false);
        PhotonNetwork.player.SetAttribute(PlayerAttributes.TEAM, 0);
        InvokeRepeating("RefreshGameBoard", 0f, 1f);
    }
    void OnLeftRoom()
    {
        ChatVik.SendRoomMessage(PhotonNetwork.player.NickName + " leave the game");
        PhotonNetwork.player.SetAttribute(PlayerAttributes.TEAM, 0);
        InitPlayerAttributes(PhotonNetwork.player,false);
    }
    void ReloadRoomScene()
    {
        return; // pour l'instant
        PhotonNetwork.DestroyAll();
        PhotonNetwork.LoadLevel("0-MainMenu");
    }



    void RefreshGameBoard()
    {
        PhotonPlayer[] plist = PhotonNetwork.playerList; // BUGFIX CAR LA LISTE PEUT CHANGER EN TRAITEMENT

        // ACTUALISE L'ENTETE DU GAMEBOARD, AFIN DE SAVOR LE NUMERO DU ROUND ACTUEL
        if (PhotonNetwork.room.GetRoomState() == GameState.WarmUp) text_RoundNumber.text = "Warmup";
        else text_RoundNumber.text = PhotonNetwork.room.GetRoomState().ToString() + " " + PhotonNetwork.room.GetAttribute(RoomAttributes.ROUNDNUMBER, 0);
        
        // RAFRAICHISSEMENT DU GAMEBOARD DES JOUEURS
        List<PlayerGBInfo> newList = playersGBInfoList.ToList();
        foreach (PlayerGBInfo PGBI in playersGBInfoList.ToList())
        {
            if (!plist.Contains(PGBI.player))
            { // remove 
                Destroy(PGBI);
                newList.Remove(PGBI);
            }
            else//refresh
            {
                PGBI.txt_state.text = getPlayerStrState(PGBI.player);
                PGBI.txt_latence.text = "-";
                PGBI.txt_score.text = PGBI.player.GetAttribute(PlayerAttributes.SCORE, 0).ToString();
                PGBI.txt_capture.text = PGBI.player.GetAttribute(PlayerAttributes.CAPTURESCORE, 0).ToString();
                PGBI.gameObject.SetActive(false);
            }
        }
        playersGBInfoList = newList.ToArray();
        //
        foreach (PhotonPlayer player in plist)
        {
            if (!playersGBInfoList.ToList().Any(w => w.player == player)) // add
            {
                PlayerGBInfo newone = Instantiate(gop_Gameboard_playerinfo, content_Gameboard.transform).GetComponent<PlayerGBInfo>();
                newone.txt_nickname.text = player.NickName;
                newone.player = player;
                newList.Add(newone);

                newone.txt_state.text = getPlayerStrState(newone.player);
                newone.txt_latence.text = "-";
                newone.txt_score.text = newone.player.GetAttribute(PlayerAttributes.SCORE, 0).ToString();
                newone.txt_capture.text = newone.player.GetAttribute(PlayerAttributes.CAPTURESCORE, 0).ToString();
                newone.gameObject.SetActive(false);
            }
        }
        playersGBInfoList = newList.ToArray();
        // ORDERING
        int j = 0;
        foreach (PlayerGBInfo item in playersGBInfoList.Where(w => w.player.getTeamID() == 1).OrderByDescending(o => o.player.GetAttribute(PlayerAttributes.SCORE, 0)))
        {
            item.gameObject.SetActive(true);
            var rect = item.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector3(135f, -12 + (-22f * j), 0f);
            j++;
        }
        j = 0;
        foreach (PlayerGBInfo item in playersGBInfoList.Where(w => w.player.getTeamID() == 2).OrderByDescending(o => o.player.GetAttribute(PlayerAttributes.SCORE, 0)))
        {
            item.gameObject.SetActive(true);
            var rect = item.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector3(135f + 276.5f, -12 + (-22f * j), 0f);
            j++;
        }

        // AFFICHAGE DU NOMBRE DE MANCHE GAGNEE POUR CHAQUE EQUIPE
        teamList[1].txt_roundswon.text = PhotonNetwork.room.GetTeamAttribute(1, TeamAttributes.ROUNDSWON, 0).ToString();
        teamList[2].txt_roundswon.text = PhotonNetwork.room.GetTeamAttribute(2, TeamAttributes.ROUNDSWON, 0).ToString();
    }

    string getPlayerStrState(PhotonPlayer player)
    {
        return (player.GetAttribute(PlayerAttributes.ISCAPTURED, false) ? "Captured" : player.GetPlayerState() == PlayerState.inGame ? "ig" : player.GetAttribute(PlayerAttributes.ISREADY, false) ? "Ready" : "-");
    }

    //============================================================================================//
    //============================================================================================//


    public void UpdateGameInfos()
    {
        // UPDATE DE LA PARTIE UNIQUEMENT POUR LE MASTERPLAYER
        if (!(PhotonNetwork.player.isGameOwner() && PhotonNetwork.room.GetRoomState() == GameState.RoundRunning)) return;
        // ON CHECK SI LE TIMER EST ATTEInt OU TOUT LES THIEFS SONT CAPTUREE
        if (PhotonNetwork.playerList.Where(s => s.getTeamID() == 1 && s.GetAttribute(PlayerAttributes.HASSPAWNED, false)).All(s => s.GetAttribute(PlayerAttributes.ISCAPTURED, false)))
        {
            PhotonNetwork.room.SetAttribute(RoomAttributes.ALLTHIEFCATCHED, true);
            ChatVik.SendRoomMessage("Cops catch all thiefs !");
        }
        if (PhotonNetwork.room.GetAttribute(RoomAttributes.ALLTHIEFCATCHED, false))
        {
            ChatVik.SendRoomMessage("COPS WIN THE ROUND");
            PhotonNetwork.room.SetTeamAttribute(2, TeamAttributes.ROUNDSWON, PhotonNetwork.room.GetTeamAttribute(2, TeamAttributes.ROUNDSWON, 0) + 1);
            foreach (PhotonPlayer p in PhotonNetwork.playerList.Where(s => s.getTeamID() == 1 || s.getTeamID() == 2))
                p.SetAttribute(PlayerAttributes.ISIMMOBILIZED, true);
            StartCoroutine(FinalizeRound(2));

        }
        else if (PhotonNetwork.ServerTimestamp - PhotonNetwork.room.GetAttribute(RoomAttributes.TIMEROUNDSTARTED, PhotonNetwork.ServerTimestamp) > durationRound)
        {
            ChatVik.SendRoomMessage("THIEVES WIN THE ROUND");
            PhotonNetwork.room.SetTeamAttribute(1, TeamAttributes.ROUNDSWON, PhotonNetwork.room.GetTeamAttribute(1, TeamAttributes.ROUNDSWON, 0) + 1);
            foreach (PhotonPlayer p in PhotonNetwork.playerList.Where(s => s.getTeamID() == 1 || s.getTeamID() == 2))
                p.SetAttribute(PlayerAttributes.ISIMMOBILIZED, true);
            StartCoroutine(FinalizeRound(1));

        }
    }
    IEnumerator WarmUp()
    {
        //INIT DES VARIABLES DE WARMUP
        PhotonNetwork.room.SetRoomState(GameState.WarmUp); // au cas ou
        InitRoomAttributes(true);
        SetImmobilizeAll(false);
        PhotonNetwork.room.SetAttribute(RoomAttributes.PLAYERSCANSPAWN, true);
      
        //ECOUTE DU NOMBRE DE JOUEUR READY
        while (!isMinimumPlayersReady()) yield return new WaitForSeconds(1f);

        // CREATION DU PREMIER ROUND
        PhotonNetwork.room.SetRoomState(GameState.BeginningRound);
        ReloadRoomScene();
    }
    IEnumerator PrepareRound()
    {
        // INIT DES VARIABLES DE NOUVEAU ROUND
        text_Timer.text ="NEW ROUND COMING";
        InitRoomAttributes(false);
        PhotonNetwork.room.SetRoomState(GameState.PrepareRound); // au cas ou
        PhotonNetwork.room.SetAttribute(RoomAttributes.PLAYERSCANSPAWN, true);
        SetImmobilizeAll(true);

        //DEPOP DES JOUEURS
        foreach (PhotonPlayer p in PhotonNetwork.playerList.Where(w => (w.getTeamID() == 1 || w.getTeamID() == 2) && w.GetAttribute(PlayerAttributes.HASSPAWNED, false)))
        {
            call_UnspawnPlayerAvatar(p);
        }

        // COMPTE A REBOURG AVANT LE DEBUT DU ROUND
        int timeRoundPreparation = PhotonNetwork.ServerTimestamp;
        foreach (PhotonPlayer p in PhotonNetwork.playerList) InitPlayerAttributes(p,true);
        ChatVik.SendRoomMessage("New round begin in " + (durationRoundFinalize/1000) + "sec");

        while (PhotonNetwork.ServerTimestamp - timeRoundPreparation < durationRoundFinalize)
        {
            yield return new WaitForSeconds(1f);
            int time = durationRoundFinalize/1000 - (PhotonNetwork.ServerTimestamp - timeRoundPreparation - 1000)/1000;
            if (time == 15 || time == 10 || time <=5) ChatVik.SendRoomMessage("New round begin in "+time+"sec"); 
        }

        // CHECK FINAL : IL Y A ASSEZ DE JOUEUR POUR JOUER
        if ( PhotonNetwork.playerList.Count(s => s.getTeamID() == 1 && s.GetAttribute(PlayerAttributes.HASSPAWNED, false)) < 1
            && PhotonNetwork.playerList.Count(s => s.getTeamID() == 2 && s.GetAttribute(PlayerAttributes.HASSPAWNED, false)) < 1)
        {
            ChatVik.SendRoomMessage("There is no players in both team ! Restart Warmup ...");
            StartCoroutine(WarmUp());
            yield break;
        }

        // INIT DES JOUEURS AU ROUND
        PhotonNetwork.room.SetAttribute(RoomAttributes.PLAYERSCANSPAWN, false);
        PhotonNetwork.room.SetRoomState(GameState.RoundRunning);
        PhotonNetwork.room.SetAttribute(RoomAttributes.ROUNDNUMBER, PhotonNetwork.room.GetAttribute(RoomAttributes.ROUNDNUMBER, 0)+1);
        PhotonNetwork.room.SetAttribute(RoomAttributes.TIMEROUNDSTARTED, PhotonNetwork.ServerTimestamp);
        ChatVik.SendRoomMessage("New round : "+ PhotonNetwork.room.GetAttribute(RoomAttributes.ROUNDNUMBER, 0));
        SetImmobilizeAll(false);    
    }
    IEnumerator FinalizeRound(int winnerTeamId)
    {
        // INIT DES VARIABLES DE NOUVEAU ROUND
        text_Timer.text ="ROUND IS ENDING";
        InitRoomAttributes(false);
        PhotonNetwork.room.SetRoomState(GameState.isRoundFinishing); // au cas ou
        PhotonNetwork.room.SetAttribute(RoomAttributes.PLAYERSCANSPAWN, false);
        SetImmobilizeAll(true);

        for (int i = 0; i < ZonesList.Count; i++)
        {
            PhotonNetwork.Destroy(ZonesList[0].GetPhotonView());
            ZonesList.RemoveAt(0);
        }

        //SETUP WINNER PANEL
        if (PhotonNetwork.room.GetTeamAttribute(winnerTeamId, TeamAttributes.ROUNDSWON,0) == 3 )
        {
            StartCoroutine(FinalizeGame(winnerTeamId));
            yield break;
        }

        // COMPTE A REBOURG AVANT LE DEBUT DU ROUND
        int timeRoundPreparation = PhotonNetwork.ServerTimestamp;
        foreach (PhotonPlayer p in PhotonNetwork.playerList) InitPlayerAttributes(p, false);
        ChatVik.SendRoomMessage("Revert teams in " + (durationRoundFinalize / 1000) + "sec");

        while (PhotonNetwork.ServerTimestamp - timeRoundPreparation < durationRoundFinalize)
        {
            yield return new WaitForSeconds(1f);
            int time = durationRoundFinalize / 1000 - (PhotonNetwork.ServerTimestamp - timeRoundPreparation - 1000) / 1000;
            if (time == 15 || time == 10 || time <= 5) ChatVik.SendRoomMessage("Revert teams in " + time + "sec");
        }

        // CHECK FINAL : IL Y A ASSEZ DE JOUEUR POUR JOUER
        if (PhotonNetwork.playerList.Count(s => s.getTeamID() == 1 && s.GetAttribute(PlayerAttributes.HASSPAWNED, false)) < 1
            && PhotonNetwork.playerList.Count(s => s.getTeamID() == 2 && s.GetAttribute(PlayerAttributes.HASSPAWNED, false)) < 1)
        {
            ChatVik.SendRoomMessage("There is no players in both team ! Restart Warmup ...");
            StartCoroutine(WarmUp());
            yield break;
        }

        //CHENGEMENT DE CAMPS
        List<PhotonPlayer> thieves = PhotonNetwork.playerList.Where(w => w.getTeamID() == 1).ToList();
        List<PhotonPlayer> cops = PhotonNetwork.playerList.Where(w => w.getTeamID() == 2).ToList();
        foreach (PhotonPlayer p in thieves) p.SetAttribute(PlayerAttributes.TEAM, 2);
        foreach (PhotonPlayer p in cops) p.SetAttribute(PlayerAttributes.TEAM, 1);

        //DEPOP DES JOUEURS
        foreach (PhotonPlayer p in PhotonNetwork.playerList.Where(w => (w.getTeamID() == 1 || w.getTeamID() == 2) && w.GetAttribute(PlayerAttributes.HASSPAWNED, false)))
        {
            call_UnspawnPlayerAvatar(p);
        }

        // INIT DES JOUEURS AU ROUND
        PhotonNetwork.room.SetRoomState(GameState.BeginningRound);

    }
    IEnumerator FinalizeGame(int winnerTeamId)
    {
        // INIT DES VARIABLES DE NOUVEAU ROUND
        text_Timer.text = "GAME IS OVER";
        InitRoomAttributes(false);
        PhotonNetwork.room.SetRoomState(GameState.isGameFinishing); // au cas ou
        PhotonNetwork.room.SetAttribute(RoomAttributes.PLAYERSCANSPAWN, false);
        SetImmobilizeAll(true);

        //AFFICHAGE WINNER
        ChatVik.SendRoomMessage("TEAM " + (winnerTeamId == 2 ? "COPS" : "THIEVES") + " WIN THE GAME");


        // COMPTE A REBOURG AVANT LE DEBUT DU ROUND
        int timeRoundPreparation = PhotonNetwork.ServerTimestamp;
        foreach (PhotonPlayer p in PhotonNetwork.playerList) InitPlayerAttributes(p, false);
        ChatVik.SendRoomMessage("Restart Game in " + (durationRoundFinalize / 1000) + "sec");

        while (PhotonNetwork.ServerTimestamp - timeRoundPreparation < durationRoundFinalize)
        {
            yield return new WaitForSeconds(1f);
            int time = durationRoundFinalize / 1000 - (PhotonNetwork.ServerTimestamp - timeRoundPreparation - 1000) / 1000;
            if (time == 15 || time == 10 || time <= 5) ChatVik.SendRoomMessage("Restart Game in " + time + "sec");
        }
        PhotonNetwork.room.SetRoomState(GameState.isWaitingNewGame);
    }

    //============================================================================================//
    //============================================================================================//



    //============================================================================================//
    //============================================================================================//

    /// <summary>
    /// Ca peut servir....
    /// </summary>
    /// <returns></returns>
    private IEnumerator MoveToGameScene()
    {
        // Temporary disable processing of futher network messages
        PhotonNetwork.isMessageQueueRunning = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name); // custom method to load the new scene by name
        while (true/*newSceneDidNotFinishLoading*/)
        {
            yield return null;
        }
        PhotonNetwork.isMessageQueueRunning = true;
    }

    //============================================================================================//
    //============================================================================================//

    // GESTION SPAWNING
    public string playerprefabname_overlaw = "Overlaw_Player";
    public string spectatorPrefabName = "Spectator";
    public void SpawnMyAvatar()
    {
        if (!PhotonNetwork.inRoom || PhotonNetwork.player.GetAttribute<bool>(PlayerAttributes.HASSPAWNED, false)) return;

        //bugfix
        ResetCameraTransform();
        //PhotonNetwork.DestroyPlayerObjects(PhotonNetwork.player);
        DestroyPlayerCharacters(PhotonNetwork.player);

        // CREATE AVATAR
        GameObject playerChar;
        Vector2 randpos = UnityEngine.Random.insideUnitCircle * 5f;
        if (PhotonNetwork.player.getTeamID() == 1 || PhotonNetwork.player.getTeamID() == 2)
        {
            playerChar = PhotonNetwork.Instantiate(this.playerprefabname_overlaw, getTeams[PhotonNetwork.player.getTeamID()].TeamSpawnLocation + new Vector3(randpos.x, 0f, randpos.y), Quaternion.identity, 0);
        }
        else
        {
            PhotonNetwork.player.SetAttribute(PlayerAttributes.TEAM, 3);
            playerChar = PhotonNetwork.Instantiate(this.spectatorPrefabName, getTeams[PhotonNetwork.player.getTeamID()].TeamSpawnLocation + new Vector3(randpos.x, 0f, randpos.y), Quaternion.identity, 0);

        }
        PhotonNetwork.player.SetAttribute(PlayerAttributes.HASSPAWNED, true);
        PhotonNetwork.player.SetPlayerState(PlayerState.inGame);

        //SETUP CAMERA
        playerChar.GetComponent<MNG_CameraController>().camera = Camera.main;
        Camera.main.transform.parent = playerChar.transform;
        Camera.main.transform.localPosition = new Vector3(0, 0, 0);
        Camera.main.transform.localEulerAngles = new Vector3(0, 0, 0);
        //LOCKING MOUSE
        MNG_MainMenu.captureMouse = true;
        ChatVik.SendRoomMessage(PhotonNetwork.player.NickName + " has spawn as " + getTeams[PhotonNetwork.player.getTeamID()].TeamName);
    }

    public void call_UnspawnPlayerAvatar(PhotonPlayer p)
    {
        photonView.RPC("rpc_UnspawnPlayerAvatar", PhotonTargets.All, new object[] { p });
    }

    [PunRPC]
    public void rpc_UnspawnPlayerAvatar(PhotonPlayer player)
    {
        if (!PhotonNetwork.inRoom || !player.GetAttribute<bool>(PlayerAttributes.HASSPAWNED, false)) return;
        
        //RESET DE LAA CAMERA UNIQUEMENT CHEZ LE CLIENT CONCERNEE
        if (PhotonNetwork.player == player)
        {
            ResetCameraTransform();
            //DESTROY AVATAR UNIQUEMENT PAR LE MASTERCLIENT
            //PhotonNetwork.DestroyPlayerObjects(player);
            DestroyPlayerCharacters(player);
            PlayerAvatar = null;
            player.SetAttribute(PlayerAttributes.HASSPAWNED, false);
        }

        

    }


    void DestroyPlayerCharacters(PhotonPlayer player)
    {
        foreach(PhotonView pv in FindObjectsOfType<PhotonView>().Where(x=> x.ownerId == player.ID && x.gameObject.tag == "Player"))
        {
            PhotonNetwork.Destroy(pv);
        }
    }
    //============================================================================================//
    //============================================================================================//
    void ResetCameraTransform()
    {
        //SETUP CAMERA
        Camera.main.transform.parent = MainMenuCameraRoot.transform;
        Camera.main.transform.localPosition = new Vector3(0, 0, -48);
        Camera.main.transform.localEulerAngles = new Vector3(40, 0, 0);
    }
    void InitTeamAttributes()
    {
        for (int i = 0; i < teamList.Length; i++)
        {
            PhotonNetwork.room.SetTeamAttribute(i,TeamAttributes.ROUNDSWON, 0);
        }
    }
    void InitRoundAttributes()
    {

    }

    void InitRoomAttributes(bool reinitgameattr)
    {
        PhotonNetwork.room.SetAttribute(RoomAttributes.PRISONOPEN, false);
        PhotonNetwork.room.SetAttribute(RoomAttributes.PLAYERSCANSPAWN, false);
        PhotonNetwork.room.SetAttribute(RoomAttributes.ALLTHIEFCATCHED, false);
        PhotonNetwork.room.SetAttribute(RoomAttributes.IMMOBILIZEALL, false);
        //PhotonNetwork.room.SetAttribute(RoomAttributes.TIMEROUNDSTARTED, PhotonNetwork.ServerTimestamp);
        
        if (reinitgameattr)
        {
            InitTeamAttributes();
            PhotonNetwork.room.SetAttribute(RoomAttributes.ROUNDNUMBER, 0);
        }
    }
    public void InitPlayerAttributes(PhotonPlayer Player,bool reinitgameattr)
    {
        if (!PhotonNetwork.inRoom) return;
        Player.SetAttribute(PlayerAttributes.ISIDLE, false);
        Player.SetAttribute(PlayerAttributes.ISREADY, false);
        Player.SetAttribute(PlayerAttributes.ISLAGGY, false);
        Player.SetAttribute(PlayerAttributes.ISROOMADMIN, false);
        Player.SetAttribute(PlayerAttributes.ISIMMOBILIZED, PhotonNetwork.room.GetAttribute(RoomAttributes.IMMOBILIZEALL, false));
        Player.SetPlayerState(PlayerState.isReadyToPlay);
        if (reinitgameattr)
        {
            Player.SetAttribute(PlayerAttributes.SCORE, 0);
            Player.SetAttribute(PlayerAttributes.CAPTURESCORE, 0);
            
            Player.SetAttribute(PlayerAttributes.ISCAPTURED, false);
            Player.SetAttribute(PlayerAttributes.INPRISONZONE, false);
            Player.SetAttribute(PlayerAttributes.testKey, "INITIED");

        }
    }
    public void SetImmobilizeAll(bool value)
    {
        if (!PhotonNetwork.inRoom) return;
        PhotonNetwork.room.SetAttribute(RoomAttributes.IMMOBILIZEALL, value);
        foreach (PhotonPlayer p in PhotonNetwork.playerList) p.SetAttribute(PlayerAttributes.ISIMMOBILIZED, value);
    }

}

[System.Serializable]
public struct Team
{
    public string TeamName;
    public Vector3 TeamSpawnLocation;
    public GameObject BtnCheckmark;
    public Button Btn_join;
    public Text txt_roundswon;
}
