// Life RP - shared data types.

#include "LifeTypes.h"

float FLifeNeedValues::Get(ELifeNeed Need) const
{
	switch (Need)
	{
	case ELifeNeed::Hunger:  return Hunger;
	case ELifeNeed::Energy:  return Energy;
	case ELifeNeed::Hygiene: return Hygiene;
	case ELifeNeed::Fun:     return Fun;
	case ELifeNeed::Social:  return Social;
	default:                 return 0.f;
	}
}

void FLifeNeedValues::Set(ELifeNeed Need, float Value)
{
	switch (Need)
	{
	case ELifeNeed::Hunger:  Hunger = Value; break;
	case ELifeNeed::Energy:  Energy = Value; break;
	case ELifeNeed::Hygiene: Hygiene = Value; break;
	case ELifeNeed::Fun:     Fun = Value; break;
	case ELifeNeed::Social:  Social = Value; break;
	default: break;
	}
}

bool FLifeNeedValues::IsZero() const
{
	return Hunger == 0.f && Energy == 0.f && Hygiene == 0.f && Fun == 0.f && Social == 0.f;
}
