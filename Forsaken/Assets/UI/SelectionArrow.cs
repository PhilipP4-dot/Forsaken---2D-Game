using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;


public class SelectionArrow : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private RectTransform[] options;
    [SerializeField] private AudioClip interactSound;
    [SerializeField] private AudioClip changeSound;
    private RectTransform rect;
    private int currentPosition = 0;
    
    private void Awake()    
    {
        rect = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            ChangePosition(-1);
        }
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            ChangePosition(1);
        }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            SoundManager.Instance.PlaySound(interactSound);
            options[currentPosition].GetComponent<Button>().onClick.Invoke();
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SoundManager.Instance.PlaySound(interactSound);
            options[currentPosition].GetComponent<Button>().onClick.Invoke();
        }
        
    }

    private void ChangePosition(int _change)
    {
        currentPosition += _change;
        if (_change != 0)
        {
            SoundManager.Instance.PlaySound(changeSound);
        }

        if (currentPosition < 0) currentPosition = options.Length - 1;
        if (currentPosition > options.Length-1) currentPosition = 0;
        rect.position = new Vector3(rect.position.x, options[currentPosition].position.y, 0);
    }
}
