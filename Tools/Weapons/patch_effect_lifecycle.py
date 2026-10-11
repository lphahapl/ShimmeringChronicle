from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def edit(file,old,new):
    p=ROOT/file;b=p.read_bytes();old=old.replace('\n','\r\n').encode();new=new.replace('\n','\r\n').encode()
    if new in b:return
    assert b.count(old)==1,(file,b.count(old));p.write_bytes(b.replace(old,new))
edit('Assets/Scripts/Logic/PlayerController.cs','    void ResetCombo()\n    {\n        comboIndex = 0;','    void ResetCombo()\n    {\n        if (player) player.EndEff();\n        comboIndex = 0;')
edit('Assets/Scripts/Logic/EnemyObj.cs','            animator.SetTrigger("Hit");','            EndEff();\n            animator.SetTrigger("Hit");')
edit('Assets/Scripts/Logic/EnemyObj.cs','        EnemyManager.Instance.Unregister(this);','        EndEff();\n        EnemyManager.Instance.Unregister(this);')
edit('Assets/Scripts/Logic/EnemyObj.cs','    private void OnDisable()\n    {','    private void OnDisable()\n    {\n        EndEff();')
edit('Assets/Scripts/Logic/EnemyObj.cs','    public void EndEff()\n    {','    public void EndEff()\n    {\n        if (data == null) return;')
edit('Assets/Scripts/Logic/EnemyAI.cs','    private void ChangeAttackStatu()\n    {','    private void ChangeAttackStatu()\n    {\n        if (enemyObj) enemyObj.EndEff();')
print('Effect end calls patched.')
