# Synthetic test fixtures

Run only against `VobaDemoTests` or explicit `voba_seed_*` database:

```powershell
.\scripts\Invoke-VobaDemo.ps1 -Task Seed
```

Dummy accounts:

| Email | Password |
| --- | --- |
| `voba-seed-owner@example.invalid` | `Voba-Dummy-2026!` |
| `voba-seed-other@example.invalid` | `Voba-Dummy-2026!` |

These are public test credentials, not Atlas database credentials. Tool uses real registration and login, then revokes its login sessions. Each account gets one synthetic `[VOBA TEST] Bean Soup` saved recipe. Shared generation cache gets canonical options and full-recipe entries for budget `23.41`, servings `2`, cuisine `Seed Fixture`, restriction `vegan`. Run again: no duplicate accounts or saved recipes. Existing account with different password stops seed before recipe/cache writes. Tool never deletes existing data.
