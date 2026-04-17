using System.Collections;

public interface IAction
{
    bool CanExecute(Character actor);
    IEnumerator Execute(Character actor);
}