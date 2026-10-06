// Type declarations for classify.mjs (Stage 0.2.1A synthetic spike).
import type { Classification, ClassificationBasis, Quality } from './operational';

export function classifyScore(score: number, threshold: number): 'DIRTY' | 'CLEANER';
export function classifySensor(input: {
  quality: Quality;
  score: number | null;
  lastValidatedScore: number | null;
  threshold: number;
}): { classification: Classification; basis: ClassificationBasis; updatesLastValidated: boolean };
