using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DialogueManager : MonoBehaviour {

    public Dialogue dialogue;
    public GameObject dialoguePanel;
    public TextMeshProUGUI displayText;
    public float typingSpeed = 0.05f;
    [Min(0.1f)]
    public float interactionRadius = 5f;
    [TextArea(2, 4)]
    public string fallbackSentence;
    //public AudioClip speakSound; 

    Queue<string> sentences;
    string activeSentence;
    //AudioSource myAudio;

    // Start is called before the first frame update
    void Start() {
        sentences = new Queue<string>();
        //myAudio = GetComponent<AudioSource>();

        ResolveDialogueReferences();

        CapsuleCollider2D trigger = GetComponent<CapsuleCollider2D>();
        if (trigger != null) {
            trigger.isTrigger = true;
            trigger.size = new Vector2(interactionRadius * 2f, interactionRadius * 2f);
        }
    }

    void StartDialogue() {
        if (dialoguePanel == null || displayText == null) {
            return;
        }

        sentences.Clear();

        if (dialogue != null && dialogue.sentenceList != null) {
            foreach(string sentence in dialogue.sentenceList) {
                if (!string.IsNullOrWhiteSpace(sentence)) {
                    sentences.Enqueue(sentence);
                }
            }
        }

        if (sentences.Count == 0 && !string.IsNullOrWhiteSpace(fallbackSentence)) {
            sentences.Enqueue(fallbackSentence);
        }

        if (sentences.Count == 0) {
            return;
        }

        DisplayNextSentence();
    }
    
    void DisplayNextSentence() {
        if (displayText == null) {
            return;
        }

        if(sentences.Count <= 0) {
            displayText.text = activeSentence;
            return;
        }

        activeSentence = sentences.Dequeue();
        displayText.text = activeSentence;

        StopAllCoroutines();
        StartCoroutine(TypeTheSentence(activeSentence));
    }

    IEnumerator TypeTheSentence(string sentence) {
        displayText.text = "";

        foreach(char letter in sentence.ToCharArray()) {
            displayText.text += letter;
            //myAudio.PlayOneShot(speakSound);
            yield return new WaitForSeconds(typingSpeed);
        }
    }

    private void OnTriggerEnter2D(Collider2D other) {
        if(other.CompareTag("Player")) {
            ResolveDialogueReferences();
            if (dialoguePanel == null || displayText == null) {
                return;
            }

            dialoguePanel.SetActive(true);
            StartDialogue();
        }
    }

    private void OnTriggerStay2D(Collider2D collision) {
        if(collision.CompareTag("Player")) {
            if(Input.GetKeyDown(KeyCode.Return) && displayText.text == activeSentence) {
                DisplayNextSentence();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision) {
        if(collision.CompareTag("Player")) {
            if (dialoguePanel != null) {
                dialoguePanel.SetActive(false);
            }
            StopAllCoroutines();
        }
    }

    public void DialogueMobile() {
        if (displayText != null && displayText.text == activeSentence) {
            DisplayNextSentence();
        }
    }

    private void ResolveDialogueReferences() {
        if (dialoguePanel != null && displayText != null) {
            return;
        }

        GameObject dialogueRoot = GameObject.Find("Dialogos");
        if (dialogueRoot == null) {
            return;
        }

        Transform panelTransform = dialogueRoot.transform.Find("Image");
        if (dialoguePanel == null && panelTransform != null) {
            dialoguePanel = panelTransform.gameObject;
        }

        if (displayText == null && panelTransform != null) {
            Transform textTransform = panelTransform.Find("DisplayText");
            if (textTransform != null) {
                displayText = textTransform.GetComponent<TextMeshProUGUI>();
            }
        }
    }


}
