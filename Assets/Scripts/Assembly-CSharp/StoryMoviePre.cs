using UnityEngine;

public class StoryMoviePre : MonoBehaviour
{
	private int mTicsCounter;

	private void Update()
	{
		mTicsCounter++;
		if (mTicsCounter == 2)
		{
			UnityEngine.SceneManagement.SceneManager.LoadScene("StoryMovie");
		}
	}
}
