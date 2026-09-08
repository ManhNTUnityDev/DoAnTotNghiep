using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class LoadingBar : MonoBehaviour
{
    [SerializeField] private Image loadingFill;

    private AsyncOperation asyncOperation;
    
    private const float LoadingTime = 3f;
    private const string GamePlaySceneName = "Game";

    private void Start()
    {
        StartCoroutine(LoadSceneAsync());
    }

    private IEnumerator LoadSceneAsync()
    {
        yield return null;

        asyncOperation = SceneManager.LoadSceneAsync(GamePlaySceneName);
        asyncOperation.allowSceneActivation = false;

        loadingFill.DOFillAmount(1, LoadingTime).SetEase(Ease.Linear).OnComplete(() =>
        {
            asyncOperation.allowSceneActivation = true;
        });
    }
}
