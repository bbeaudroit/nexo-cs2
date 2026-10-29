namespace CsEsp;
public static class MovementBindings
{
    static bool JumpAllowed(int key)=>key==32||key is >=48 and <=57||key is >=65 and <=90&&!IsDirection(key)||key is >=112 and <=135;
    static bool HoldAllowed(int key)=>key is 4 or 5 or 6 or 16 or 32||key is >=48 and <=57||key is >=65 and <=90&&!IsDirection(key)||key is >=112 and <=135;
    static bool IsDirection(int key)=>key is 65 or 68 or 83 or 87;
    public static (int Hold,int Jump) Normalize(int hold,int jump,int menu,int toggle,int privacy)
    {
        bool Collision(int key)=>key==menu||key==toggle||key==privacy;
        if(!JumpAllowed(jump)||Collision(jump))jump=0;
        if(!HoldAllowed(hold)||Collision(hold)||hold==jump)hold=0;
        return(hold,jump);
    }
}
