using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class LevelComplete : MonoBehaviour
{
    [SerializeField] private float levelCompleteDelay = 2f;

    public void CompleteLevel()
    {
        StartCoroutine(LoadNextSceneWithDelay());
    }

    private IEnumerator LoadNextSceneWithDelay()
    {
        yield return new WaitForSeconds(levelCompleteDelay);

        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        int nextSceneIndex = currentSceneIndex + 1;
        int totalScenes = SceneManager.sceneCountInBuildSettings;

        if (nextSceneIndex < totalScenes)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            SceneManager.LoadScene(0);
        }
    }
}