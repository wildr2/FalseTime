using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScoreUI : MonoBehaviour
{
    public Text win_text, score_text;
    private GameManager gm;


    // PUBLIC ACCESSORS

    // PUBLIC MODIFIERS

    // PRIVATE / PROTECTED MODIFIERS

    private void Awake()
    {
        gm = FindObjectOfType<GameManager>();
        gm.on_history_change += OnHistoryChange;
        gm.on_player_registered += (Player p) => UpdateScore();
        gm.on_win += OnWin;
    }

    private void OnWin(int winner, float win_time)
    {
        win_text.transform.parent.gameObject.SetActive(true);
        win_text.text = gm.player_names[winner].ToUpper() + " WINS";
    }
    private void OnHistoryChange()
    {
        UpdateScore();
    }

    private void UpdateScore()
    {
        score_text.text = "";
        foreach (Player player in gm.GetPlayers().Values)
        {
            score_text.text += Tools.ColorRichTxt(gm.GetPlayerScore(player).ToString(), gm.player_colors[player.player_id]);
            score_text.text += " - ";
        }
        if (score_text.text.Length < 2) return;
        score_text.text = score_text.text.Substring(0, score_text.text.Length - 2);
    }
}
