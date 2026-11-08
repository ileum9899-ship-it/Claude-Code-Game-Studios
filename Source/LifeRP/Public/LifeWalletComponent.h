// Life RP - money.

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "LifeWalletComponent.generated.h"

/** Cash in pocket plus a bank account that pays rent and earns interest at midnight. */
UCLASS(ClassGroup = (Life), meta = (BlueprintSpawnableComponent))
class LIFERP_API ULifeWalletComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	ULifeWalletComponent();

	UFUNCTION(BlueprintPure, Category = "Life|Money") int32 GetCash() const { return Cash; }
	UFUNCTION(BlueprintPure, Category = "Life|Money") int32 GetBank() const { return Bank; }

	/** True if cash + positive bank balance covers Amount. */
	UFUNCTION(BlueprintPure, Category = "Life|Money") bool CanAfford(int32 Amount) const;

	/** Pays from cash first, then by card from the bank. Returns false (and pays nothing) if unaffordable. */
	UFUNCTION(BlueprintCallable, Category = "Life|Money") bool TrySpend(int32 Amount, const FString& Reason);

	UFUNCTION(BlueprintCallable, Category = "Life|Money") void AddCash(int32 Amount);
	UFUNCTION(BlueprintCallable, Category = "Life|Money") void AddBank(int32 Amount);

	/** Moves all cash into the bank. Returns the amount moved. */
	UFUNCTION(BlueprintCallable, Category = "Life|Money") int32 DepositAll();

	/** Moves up to Amount from bank to cash. Returns the amount moved. */
	UFUNCTION(BlueprintCallable, Category = "Life|Money") int32 Withdraw(int32 Amount);

	/** Restores balances from a save. */
	void SetBalances(int32 InCash, int32 InBank) { Cash = InCash; Bank = InBank; }

protected:
	virtual void BeginPlay() override;

private:
	UFUNCTION() void HandleNewDay(int32 NewDay);

	UPROPERTY(VisibleInstanceOnly, Category = "Life|Money") int32 Cash = 0;
	UPROPERTY(VisibleInstanceOnly, Category = "Life|Money") int32 Bank = 0;
};
