using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MatchUI : MonoBehaviour
{
    public Text win_text;
    public Image player_flag;
    public Text match_text;
    public RectTransform info_column;

    public Text score_block_prefab;

    private Text[] score_blocks;
    private GameManager gm;


    // PUBLIC ACCESSORS

    // PUBLIC MODIFIERS

    // PRIVATE / PROTECTED MODIFIERS

    private void Awake()
    {
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
            player_flag.color = gm.player_colors[p.player_id];
        }

        // Match type text
        match_text.text = string.Format("FIRST TO {0} FLAGS", gm.GetFlagsToWin());

        // Create score blocks
        score_blocks = new Text[gm.GetNumPlayers()];
        for (int i = 0; i < score_blocks.Length; ++i)
        {
            Text sb = Instantiate(score_block_prefab);
            sb.transform.SetParent(info_column, false);
            sb.text = gm.GetPlayerScore(i).ToString();
            Image flag = sb.GetComponentInChildren<Image>();
            flag.color = gm.player_colors[i];

            score_blocks[i] = sb;
        }

        // Events
        gm.on_new_flag += OnNewFlag;
        gm.on_win += OnWin;
    }
    
    private void OnWin(int winner, float win_time)
    {
        win_text.transform.parent.gameObject.SetActive(true);
        win_text.text = gm.player_names[winner].ToUpper() + " WINS";
    }
    private void OnNewFlag(NewFlagEvent e)
    {
        score_blocks[e.player_id].text = gm.GetPlayerScore(e.player_id).ToString();
    }
}
