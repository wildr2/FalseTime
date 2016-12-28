using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MatchUI : MonoBehaviour
{
    // UI Refs
    public Text win_text;
    public Image player_flag;
    public Text match_text;
    public RectTransform info_column;
    public Text score_block_prefab;

    private Text[] score_blocks;

    // References
    private GameManager gm;
    private DataManager dm;


    // PUBLIC ACCESSORS

    // PUBLIC MODIFIERS

    // PRIVATE / PROTECTED MODIFIERS

    private void Awake()
    {
        dm = DataManager.Instance;
        gm = FindObjectOfType<GameManager>();
        gm.on_all_players_registered += Initialize;
    }

    private void Initialize()
    {
        // Player flag
        Player p = gm.GetLocalHumanPlayer();
        player_flag.gameObject.SetActive(p != null);
        if (p != null)
        {
            player_flag.color = dm.GetPlayerColor(p.PlayerID);
        }

        // Match type text
        match_text.gameObject.SetActive(true);
        match_text.text = string.Format("FIRST TO {0} FLAGS", dm.flags_to_win);

        // Create score blocks
        score_blocks = new Text[dm.GetNumPlayers()];
        for (int i = 0; i < score_blocks.Length; ++i)
        {
            Text sb = Instantiate(score_block_prefab);
            sb.transform.SetParent(info_column, false);
            sb.text = gm.GetPlayerScore(i).ToString();
            Image flag = sb.GetComponentInChildren<Image>();
            flag.color = dm.GetPlayerColor(i);

            score_blocks[i] = sb;
        }

        // Events
        gm.on_player_score += OnPlayerScore;
        gm.on_player_win += OnPlayerWin;
    }
    
    private void OnPlayerWin(int winner)
    {
        win_text.transform.parent.gameObject.SetActive(true);
        win_text.text = dm.player_names[winner].ToUpper() + " WINS";
    }
    private void OnPlayerScore(int player_id)
    {
        score_blocks[player_id].text = gm.GetPlayerScore(player_id).ToString();
    }
}
