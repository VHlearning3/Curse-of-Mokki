# Curse of Mökki — instructions for Claude

First person horror stealth game. Unity 6000.3.24f1, URP. Two developers, Otto and Vili, each working with their own Claude session on their own PC.

## Shared memory: the Obsidian vault

Coordination between the two Claude sessions lives in a separate notes repo, not in this one:

- Repo: https://github.com/riceinchicken/Curse-of-m-kki-obsidian
- On Otto's PC: `C:\Users\Pelifarmi\Documents\Obsidian\Escape mökki`
- On Vili's PC: `C:\Users\Pelifarmi\Documents\Obsidian\Curse-of-m-kki-obsidian`
- On another PC: ask the user where the vault is cloned if you can't find it.

The note **`Claude log.md`** in that vault is the source of truth for who made which script. Follow the rules at the top of that note. In short:

1. Before starting work, `git pull` the vault and read `Claude log.md`; also pull this repo.
2. Before creating a script, check the Script registry and the ownership table there. Don't create a script that exists or that belongs to the other person's area.
3. Before longer work, add an In progress row and push the vault first.
4. After creating or changing a script, update the Script registry and Session log, then commit and push the vault.
5. Don't edit the other person's scripts without the user saying so; write a request in the log instead.

Design reference is also in the vault: `Curse of the mökki GDD.md` and `Work plans/`.

## Who is the user

Both PCs use the Windows user folder `Pelifarmi`, so the folder name does not tell you who the user is. Run `git config user.name` in this repo: "Otto" means you are working for Otto, "Vili" means Vili. If it returns anything else or isn't clear, ask, since ownership depends on it.
